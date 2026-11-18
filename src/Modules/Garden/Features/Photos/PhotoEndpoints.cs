using GardenBuilder.Contracts.Designs;
using GardenBuilder.Contracts.Gardens;
using GardenBuilder.Contracts.Identity;
using GardenBuilder.Contracts.Scenes;
using GardenBuilder.Modules.Garden.Domain;
using GardenBuilder.Modules.Garden.Infrastructure;
using GardenBuilder.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace GardenBuilder.Modules.Garden.Features.Photos;

/// <summary>
/// The customer's photos of their garden: upload (re-encoded, at most six), list with short-lived links, where each
/// was taken, and removal. A garden that belongs to someone else answers exactly like one that does not exist.
/// </summary>
internal static class PhotoEndpoints
{
    /// <summary>A camera may stand this far outside the plot: photos are often taken from the street.</summary>
    public const double CameraOutsidePlot = 5;

    public sealed record CameraResponse(double X, double Y, double Heading);

    public sealed record PhotoResponse(Guid Id, int Width, int Height, Uri Url, CameraResponse? Camera);

    public sealed record CameraBody(double X, double Y, double Heading);

    public sealed record CodeResponse(string Code);

    public static RouteGroupBuilder MapPhotos(this RouteGroupBuilder group)
    {
        group.MapPost("/{gardenId:guid}/photos", UploadAsync)
            .WithName("UploadPhoto")
            // Room for a 15 MB photo and the form around it; larger files are answered "too-large" below.
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024 * 1024));
        group.MapGet("/{gardenId:guid}/photos", ListAsync).WithName("ListPhotos");
        group.MapPut("/{gardenId:guid}/photos/{photoId:guid}/camera", PlaceCameraAsync).WithName("PlacePhotoCamera");
        group.MapDelete("/{gardenId:guid}/photos/{photoId:guid}", DeleteAsync).WithName("DeletePhoto");
        return group;
    }

    private static async Task<bool> OwnsAsync(Guid gardenId, ICurrentCustomer customer, GardenDbContext db, CancellationToken cancellationToken) =>
        await customer.FindAsync(cancellationToken) is { } customerId
        && await db.Gardens.AnyAsync(g => g.Id == gardenId && g.CustomerId == customerId, cancellationToken);

    private static PhotoResponse ToResponse(GardenPhoto photo, PhotoStore store) => new(
        photo.Id,
        photo.Width,
        photo.Height,
        store.ReadLink(photo.BlobName),
        photo.Camera is { } c ? new CameraResponse(c.Position.X, c.Position.Y, c.HeadingDegrees) : null);

    private static async Task<IResult> UploadAsync(
        Guid gardenId,
        HttpRequest request,
        ICurrentCustomer customer,
        GardenDbContext db,
        PhotoStore store,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (!await OwnsAsync(gardenId, customer, db, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var file = request.HasFormContentType ? (await request.ReadFormAsync(cancellationToken)).Files.GetFile("file") : null;
        if (file is null)
        {
            return TypedResults.BadRequest(new CodeResponse("not-an-image"));
        }

        if (file.Length > PhotoEncoder.MaxBytes)
        {
            return TypedResults.Json(new CodeResponse("too-large"), statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        EncodedPhoto? encoded;
        await using (var stream = file.OpenReadStream())
        {
            encoded = PhotoEncoder.Encode(stream);
        }

        if (encoded is null)
        {
            return TypedResults.BadRequest(new CodeResponse("not-an-image"));
        }

        var photoId = Ids.New();
        var strategy = db.Database.CreateExecutionStrategy();
        var photo = await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            // Locks the garden row, so two uploads at once cannot both become photo number six.
            await db.Database.ExecuteSqlAsync($"SELECT id FROM garden.gardens WHERE id = {gardenId} FOR UPDATE", cancellationToken);
            if (await db.Photos.CountAsync(p => p.GardenId == gardenId, cancellationToken) >= GardenPhoto.MaxPerGarden)
            {
                return null;
            }

            var created = GardenPhoto.Create(photoId, gardenId, encoded.Width, encoded.Height, time.GetUtcNow());
            db.Photos.Add(created);
            await db.SaveChangesAsync(cancellationToken);
            // Inside the transaction: if the upload fails, the row is rolled back and no photo points at nothing.
            await store.UploadAsync(created.BlobName, encoded.Jpeg, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return created;
        });

        return photo is null
            ? TypedResults.Conflict(new CodeResponse("too-many-photos"))
            : TypedResults.Created($"/api/gardens/{gardenId}/photos/{photo.Id}", ToResponse(photo, store));
    }

    private static async Task<IResult> ListAsync(
        Guid gardenId, ICurrentCustomer customer, GardenDbContext db, PhotoStore store, CancellationToken cancellationToken)
    {
        if (!await OwnsAsync(gardenId, customer, db, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var photos = await db.Photos.AsNoTracking()
            .Where(p => p.GardenId == gardenId)
            .OrderBy(p => p.CreatedAt).ThenBy(p => p.Id)
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(photos.Select(p => ToResponse(p, store)).ToList());
    }

    private static async Task<IResult> PlaceCameraAsync(
        Guid gardenId,
        Guid photoId,
        CameraBody body,
        ICurrentCustomer customer,
        GardenDbContext db,
        IDesignWorkingCopies designs,
        CancellationToken cancellationToken)
    {
        if (!await OwnsAsync(gardenId, customer, db, cancellationToken)
            || await db.Photos.SingleOrDefaultAsync(p => p.Id == photoId && p.GardenId == gardenId, cancellationToken) is not { } photo)
        {
            return TypedResults.NotFound();
        }

        var position = new Point2(body.X, body.Y);
        if (!double.IsFinite(body.X) || !double.IsFinite(body.Y) || !double.IsFinite(body.Heading)
            || await PlotOfAsync(gardenId, designs, cancellationToken) is not { } plot
            || plot.DistanceTo(position) > CameraOutsidePlot)
        {
            return TypedResults.BadRequest(new CodeResponse("camera-outside-plot"));
        }

        photo.PlaceCamera(new CameraPose(position, body.Heading));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Polygon?> PlotOfAsync(Guid gardenId, IDesignWorkingCopies designs, CancellationToken cancellationToken) =>
        (await designs.LatestForGardenAsync(gardenId, cancellationToken))?.Scene.Elements.OfType<PlotElement>().FirstOrDefault() is { } plot
            ? new Polygon(plot.Points)
            : null;

    private static async Task<IResult> DeleteAsync(
        Guid gardenId, Guid photoId, ICurrentCustomer customer, GardenDbContext db, PhotoStore store, CancellationToken cancellationToken)
    {
        if (!await OwnsAsync(gardenId, customer, db, cancellationToken)
            || await db.Photos.SingleOrDefaultAsync(p => p.Id == photoId && p.GardenId == gardenId, cancellationToken) is not { } photo)
        {
            return TypedResults.NotFound();
        }

        db.Photos.Remove(photo);
        await db.SaveChangesAsync(cancellationToken);
        await store.DeleteAsync(photo.BlobName, cancellationToken);
        return TypedResults.NoContent();
    }
}
