using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace GardenBuilder.Modules.Garden.Features.Photos;

/// <summary>The photos' blobs, in a private container. Only this class talks to Blob Storage.</summary>
internal sealed class PhotoStore(BlobServiceClient blobs, TimeProvider time)
{
    public const string ContainerName = "garden-photos";

    /// <summary>How long a link to a photo works (docs/design.md: short-lived links).</summary>
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(10);

    private BlobContainerClient Container => blobs.GetBlobContainerClient(ContainerName);

    public async Task UploadAsync(string blobName, byte[] jpeg, CancellationToken cancellationToken)
    {
        await Container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        await Container.GetBlobClient(blobName).UploadAsync(
            BinaryData.FromBytes(jpeg),
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "image/jpeg" } },
            cancellationToken);
    }

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

    /// <summary>A read-only link to one photo that stops working after <see cref="LinkLifetime"/>.</summary>
    public Uri ReadLink(string blobName) =>
        Container.GetBlobClient(blobName).GenerateSasUri(BlobSasPermissions.Read, time.GetUtcNow().Add(LinkLifetime));
}
