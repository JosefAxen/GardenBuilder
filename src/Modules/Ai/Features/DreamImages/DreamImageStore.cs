using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace GardenBuilder.Modules.Ai.Features.DreamImages;

/// <summary>
/// The dream images and their renders, in a private container of their own. Blobs live under the garden's id so they
/// can be deleted with the garden. Only the Ai module reads this container.
/// </summary>
internal sealed class DreamImageStore(BlobServiceClient blobs, TimeProvider time)
{
    public const string ContainerName = "dream-images";

    /// <summary>How long a link to an image works (docs/design.md: short-lived links).</summary>
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(10);

    private BlobContainerClient Container => blobs.GetBlobContainerClient(ContainerName);

    /// <summary>The 3D render the customer saw, uploaded before the job runs.</summary>
    public static string RenderBlob(Guid gardenId, Guid jobId) => $"{gardenId}/{jobId}/render.jpg";

    /// <summary>The finished dream image.</summary>
    public static string ImageBlob(Guid gardenId, Guid jobId) => $"{gardenId}/{jobId}/image.jpg";

    public async Task UploadAsync(string blobName, byte[] jpeg, CancellationToken cancellationToken)
    {
        await Container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        await Container.GetBlobClient(blobName).UploadAsync(
            BinaryData.FromBytes(jpeg),
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "image/jpeg" } },
            cancellationToken);
    }

    /// <summary>The blob's bytes, or null when it does not exist.</summary>
    public async Task<byte[]?> ReadAsync(string blobName, CancellationToken cancellationToken)
    {
        try
        {
            var content = await Container.GetBlobClient(blobName).DownloadContentAsync(cancellationToken);
            return content.Value.Content.ToArray();
        }
        catch (RequestFailedException e) when (e.Status == 404)
        {
            return null;
        }
    }

    public Task DeleteAsync(string blobName, CancellationToken cancellationToken) =>
        Container.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: cancellationToken);

    /// <summary>A read-only link to one blob that stops working after <see cref="LinkLifetime"/>.</summary>
    public Uri LinkFor(string blobName) =>
        Container.GetBlobClient(blobName).GenerateSasUri(BlobSasPermissions.Read, time.GetUtcNow().Add(LinkLifetime));

    /// <summary>
    /// Like <see cref="LinkFor"/>, but the storage answers "save as a file". The storage is on another address than the
    /// page, where browsers ignore &lt;a download&gt;, so the link itself has to ask.
    /// </summary>
    public Uri DownloadLinkFor(string blobName)
    {
        var sas = new BlobSasBuilder(BlobSasPermissions.Read, time.GetUtcNow().Add(LinkLifetime))
        {
            BlobContainerName = ContainerName,
            BlobName = blobName,
            Resource = "b",
            ContentDisposition = "attachment; filename=\"drombild.jpg\"",
        };
        return Container.GetBlobClient(blobName).GenerateSasUri(sas);
    }
}
