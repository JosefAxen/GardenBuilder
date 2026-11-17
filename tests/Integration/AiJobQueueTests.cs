using System.Text.Json;
using GardenBuilder.Modules.Ai.Domain;
using GardenBuilder.Modules.Ai.Features.Queue;
using GardenBuilder.Modules.Ai.Infrastructure;
using GardenBuilder.SharedKernel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GardenBuilder.IntegrationTests;

/// <summary>The AI job queue in ai.jobs: claims with row locks, leases, retries and the third failure.</summary>
public sealed class AiJobQueueTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly ManualTimeProvider _clock = new();
    private WebApplicationFactory<Program> _app = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _app = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(_clock)));
        // Each test starts with an empty queue.
        using var scope = _app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AiDbContext>().Set<AiJob>().ExecuteDeleteAsync(Ct);
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    private async Task<T> InScope<T>(Func<JobQueue, Task<T>> work)
    {
        using var scope = _app.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<JobQueue>());
    }

    private async Task InScope(Func<JobQueue, Task> work)
    {
        using var scope = _app.Services.CreateScope();
        await work(scope.ServiceProvider.GetRequiredService<JobQueue>());
    }

    private async Task<Guid> AddAsync()
    {
        var job = AiJob.Create("connection-test", null, null, null, JsonSerializer.SerializeToElement(new { }), _clock.GetUtcNow());
        await InScope(q => q.AddAsync(job, Ct));
        _clock.Advance(TimeSpan.FromMilliseconds(1));
        return job.Id;
    }

    private Task<AiJob?> ClaimAsync() => InScope(q => q.ClaimNextAsync(Ct));

    private Task<AiJob?> FindAsync(Guid id) => InScope(q => q.FindAsync(id, Ct));

    [Fact]
    public async Task Claim_takes_the_oldest_queued_job_and_leases_it_for_five_minutes()
    {
        var first = await AddAsync();
        await AddAsync();

        var claimed = await ClaimAsync();

        Assert.NotNull(claimed);
        Assert.Equal(first, claimed.Id);
        Assert.Equal(AiJobStatus.Running, claimed.Status);
        Assert.Equal(1, claimed.Attempts);
        // PostgreSQL keeps microseconds; the clock has ticks.
        Assert.Equal(_clock.GetUtcNow() + TimeSpan.FromMinutes(5), claimed.LockedUntil!.Value, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Two_claims_at_once_never_take_the_same_job()
    {
        for (var i = 0; i < 10; i++)
        {
            await AddAsync();
        }

        var claims = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(ClaimAsync, Ct)));

        var taken = claims.Where(c => c is not null).Select(c => c!.Id).ToList();
        Assert.Equal(10, taken.Count);
        Assert.Equal(10, taken.Distinct().Count());
    }

    [Fact]
    public async Task A_job_whose_lease_ran_out_is_claimed_again()
    {
        var id = await AddAsync();
        await ClaimAsync();
        Assert.Null(await ClaimAsync());

        _clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        var again = await ClaimAsync();

        Assert.Equal(id, again?.Id);
        Assert.Equal(2, again?.Attempts);
    }

    [Fact]
    public async Task Failed_attempts_wait_10_then_60_seconds()
    {
        var id = await AddAsync();
        await ClaimAsync();
        await InScope(q => q.FailAsync(id, "Tillfälligt fel.", retry: true, Ct));

        _clock.Advance(TimeSpan.FromSeconds(9));
        Assert.Null(await ClaimAsync());
        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(id, (await ClaimAsync())?.Id);

        await InScope(q => q.FailAsync(id, "Tillfälligt fel.", retry: true, Ct));
        _clock.Advance(TimeSpan.FromSeconds(59));
        Assert.Null(await ClaimAsync());
        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(id, (await ClaimAsync())?.Id);
    }

    [Fact]
    public async Task The_third_failure_is_final()
    {
        var id = await AddAsync();
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            Assert.Equal(id, (await ClaimAsync())?.Id);
            await InScope(q => q.FailAsync(id, "Svaret gick inte att läsa.", retry: true, Ct));
            _clock.Advance(TimeSpan.FromMinutes(2));
        }

        var job = await FindAsync(id);
        Assert.Equal(AiJobStatus.Failed, job?.Status);
        Assert.Equal("Svaret gick inte att läsa.", job?.Failure);
        Assert.NotNull(job?.CompletedAt);
        _clock.Advance(TimeSpan.FromHours(1));
        Assert.Null(await ClaimAsync());
    }

    [Fact]
    public async Task A_failure_without_retry_is_final_at_once()
    {
        var id = await AddAsync();
        await ClaimAsync();

        await InScope(q => q.FailAsync(id, "Ingen API-nyckel för Claude är inställd.", retry: false, Ct));

        Assert.Equal(AiJobStatus.Failed, (await FindAsync(id))?.Status);
    }

    [Fact]
    public async Task A_lease_that_runs_out_on_the_third_attempt_ends_failed()
    {
        var id = await AddAsync();
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            Assert.Equal(id, (await ClaimAsync())?.Id);
            _clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        }

        Assert.Null(await ClaimAsync());
        var job = await FindAsync(id);
        Assert.Equal(AiJobStatus.Failed, job?.Status);
        Assert.Equal("Jobbet avbröts tre gånger.", job?.Failure);
    }

    [Fact]
    public async Task Succeed_stores_the_result()
    {
        var id = await AddAsync();
        await ClaimAsync();

        await InScope(q => q.SucceedAsync(id, JsonSerializer.SerializeToElement(new { answer = "En trädgård är en plats för växter." }), Ct));

        var job = await FindAsync(id);
        Assert.Equal(AiJobStatus.Succeeded, job?.Status);
        Assert.Equal("En trädgård är en plats för växter.", job?.Result?.GetProperty("answer").GetString());
        Assert.Equal(_clock.GetUtcNow(), job!.CompletedAt!.Value, TimeSpan.FromMilliseconds(1));
        Assert.Null(job?.LockedUntil);
    }

    [Fact]
    public async Task New_job_ids_are_uuid_v7()
    {
        var id = await AddAsync();

        Assert.Equal(7, id.Version);
        Assert.NotEqual(Guid.Empty, Ids.New());
    }
}
