using System.Collections.Concurrent;
using RaceDay.Api.Services;

namespace RaceDay.Tests;

/// <summary>Test double for Azure Blob Storage.</summary>
public class InMemoryBlobStorage : IBlobStorageService
{
    private readonly ConcurrentDictionary<string, (byte[] Data, string ContentType)> _blobs = new();

    public bool IsConfigured => true;
    public int Count => _blobs.Count;

    public async Task UploadAsync(string blobName, Stream content, string contentType, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        _blobs[blobName] = (ms.ToArray(), contentType);
    }

    public Task<BlobFile?> DownloadAsync(string blobName, CancellationToken ct = default) =>
        Task.FromResult<BlobFile?>(_blobs.TryGetValue(blobName, out var b)
            ? new BlobFile(new MemoryStream(b.Data), b.ContentType)
            : null);

    public Task DeleteAsync(string blobName, CancellationToken ct = default)
    {
        _blobs.TryRemove(blobName, out _);
        return Task.CompletedTask;
    }
}
