using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using GardenBuilder.Contracts.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GardenBuilder.IntegrationTests;

/// <summary>Computes the six-digit code an authenticator app shows (RFC 6238: HMAC-SHA1, 30 seconds, 6 digits).</summary>
internal static class Totp
{
    public static string Code(string sharedKey, DateTimeOffset? at = null)
    {
        var key = Base32(sharedKey.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant());
        var counter = (at ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() / 30;
        var message = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(message);
        }

#pragma warning disable CA5350 // RFC 6238 specifies HMAC-SHA1; this only mirrors what the authenticator app does.
        var hash = HMACSHA1.HashData(key, message);
#pragma warning restore CA5350
        var offset = hash[^1] & 0x0f;
        var binary = ((hash[offset] & 0x7f) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static byte[] Base32(string text)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = 0;
        var value = 0;
        var output = new List<byte>();
        foreach (var c in text.TrimEnd('='))
        {
            value = (value << 5) | alphabet.IndexOf(c, StringComparison.Ordinal);
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)(value >> (bits - 8)));
                bits -= 8;
            }
        }

        return [.. output];
    }
}

/// <summary>A clock the test moves by hand.</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

/// <summary>Collects every log message the API writes, to check that no secrets end up in logs.</summary>
internal sealed class LogCapture : ILoggerProvider
{
    public ConcurrentQueue<string> Messages { get; } = new();

    public ILogger CreateLogger(string categoryName) => new Logger(this);

    public void Dispose()
    {
    }

    private sealed class Logger(LogCapture capture) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            capture.Messages.Enqueue($"{formatter(state, exception)} {exception}");
    }
}

/// <summary>One browser of a staff member, talking to the staff login API.</summary>
internal sealed class StaffBrowser(WebApplicationFactory<Program> factory) : IDisposable
{
    public const string NewPassword = "ett långt nytt lösenord";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public HttpClient Client { get; } = IdentityTestHelpers.HttpsClient(factory);

    public static async Task<(string Email, TemporaryPassword Temporary)> CreateStaffAsync(WebApplicationFactory<Program> factory, StaffRole role)
    {
        var email = $"{Guid.NewGuid():N}@firma.se";
        using var scope = factory.Services.CreateScope();
        var temporary = await scope.ServiceProvider.GetRequiredService<IStaffAccounts>().CreateAsync(email, "Test Person", role, Token);
        return (email, temporary);
    }

    public Task<HttpResponseMessage> PostAsync(string path, object body) =>
        Client.PostAsJsonAsync(new Uri("/api/identity/staff/" + path, UriKind.Relative), body, Token);

    public Task<HttpResponseMessage> GetAsync(string path) => Client.GetAsync(new Uri("/api/identity/staff/" + path, UriKind.Relative), Token);

    public async Task<JsonElement> MeAsync() =>
        await Client.GetFromJsonAsync<JsonElement>(new Uri("/api/identity/me", UriKind.Relative), Token);

    /// <summary>Password, new password, two-factor set up: returns the shared key and the recovery codes.</summary>
    public async Task<(string SharedKey, string[] RecoveryCodes)> FirstLoginAsync(string email, string temporaryPassword)
    {
        await Ok(await PostAsync("login", new { email, password = temporaryPassword }));
        await Ok(await PostAsync("password", new { newPassword = NewPassword }));
        var setup = await (await Ok(await GetAsync("two-factor/setup"))).Content.ReadFromJsonAsync<JsonElement>(Token);
        var sharedKey = setup.GetProperty("sharedKey").GetString()!;
        var confirmed = await (await Ok(await PostAsync("two-factor/setup", new { code = Totp.Code(sharedKey) }))).Content.ReadFromJsonAsync<JsonElement>(Token);
        return (sharedKey, [.. confirmed.GetProperty("recoveryCodes").EnumerateArray().Select(c => c.GetString()!)]);
    }

    public static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Token)).GetProperty("code").GetString();

    public static async Task<string?> NextAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Token)).GetProperty("next").GetString();

    public void Dispose() => Client.Dispose();

    private static async Task<HttpResponseMessage> Ok(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"{response.RequestMessage?.RequestUri} gave {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

        return response;
    }
}
