using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace RaceDay.Api.Services;

public class BlobStorageOptions
{
    /// <summary>Azure Storage connection string (or "UseDevelopmentStorage=true" for the Azurite emulator).</summary>
    public string? ConnectionString { get; set; }
    public string ContainerName { get; set; } = "event-banners";
}

public record BlobFile(Stream Content, string ContentType);

/// <summary>All file storage goes through this abstraction - only the API ever talks to Azure.</summary>
public interface IBlobStorageService
{
    bool IsConfigured { get; }
    Task UploadAsync(string blobName, Stream content, string contentType, CancellationToken ct = default);
    Task<BlobFile?> DownloadAsync(string blobName, CancellationToken ct = default);
    Task DeleteAsync(string blobName, CancellationToken ct = default);
}

public class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient? _container;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _containerReady;

    public AzureBlobStorageService(IOptions<BlobStorageOptions> options)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.ConnectionString)) return;

        // Fail fast rather than hanging the request if the storage account is unreachable
        var clientOptions = new BlobClientOptions
        {
            Retry = { MaxRetries = 2, Delay = TimeSpan.FromMilliseconds(500), NetworkTimeout = TimeSpan.FromSeconds(15) }
        };
        _container = new BlobContainerClient(o.ConnectionString, o.ContainerName, clientOptions);
    }

    public bool IsConfigured => _container is not null;

    private async Task<BlobContainerClient> ContainerAsync(CancellationToken ct)
    {
        if (_container is null) throw new InvalidOperationException("Azure Blob Storage is not configured.");
        if (_containerReady) return _container;

        await _gate.WaitAsync(ct);
        try
        {
            if (!_containerReady)
            {
                await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
                _containerReady = true;
            }
        }
        finally { _gate.Release(); }
        return _container;
    }

    public async Task UploadAsync(string blobName, Stream content, string contentType, CancellationToken ct = default)
    {
        var container = await ContainerAsync(ct);
        var options = new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } };
        await container.GetBlobClient(blobName).UploadAsync(content, options, ct);
    }

    public async Task<BlobFile?> DownloadAsync(string blobName, CancellationToken ct = default)
    {
        var container = await ContainerAsync(ct);
        try
        {
            var response = await container.GetBlobClient(blobName).DownloadStreamingAsync(cancellationToken: ct);
            return new BlobFile(response.Value.Content, response.Value.Details.ContentType);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string blobName, CancellationToken ct = default)
    {
        var container = await ContainerAsync(ct);
        await container.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: ct);
    }
}

/// <summary>Checks the real file signature so a renamed .exe can't be uploaded as "image/png".</summary>
public static class ImageSniffer
{
    public static (string ContentType, string Extension)? Detect(ReadOnlySpan<byte> h)
    {
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return ("image/jpeg", ".jpg");
        if (h.Length >= 8 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47 &&
            h[4] == 0x0D && h[5] == 0x0A && h[6] == 0x1A && h[7] == 0x0A) return ("image/png", ".png");
        if (h.Length >= 6 && h[0] == 'G' && h[1] == 'I' && h[2] == 'F' && h[3] == '8') return ("image/gif", ".gif");
        if (h.Length >= 12 && h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F' &&
            h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P') return ("image/webp", ".webp");
        return null;
    }
}
