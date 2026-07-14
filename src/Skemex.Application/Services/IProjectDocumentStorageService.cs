namespace Skemex.Application.Services;

public interface IProjectDocumentStorageService
{
    Task<string> CreateAsync(
        Guid tenantId,
        Guid projectId,
        Stream content,
        string contentType,
        string? fileName,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Public CDN URL when configured; otherwise a MinIO/local download URL (presigned on Production).
    /// </summary>
    Task<string?> GetDownloadUrlAsync(string? storageKey, CancellationToken cancellationToken = default);
}
