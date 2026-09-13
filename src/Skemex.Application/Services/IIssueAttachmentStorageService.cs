namespace Skemex.Application.Services;

public interface IIssueAttachmentStorageService
{
    Task<string> CreateAsync(
        Guid tenantId,
        Guid projectId,
        Guid taskId,
        Stream content,
        string contentType,
        string? fileName,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);

    Task<string?> GetDownloadUrlAsync(string? storageKey, CancellationToken cancellationToken = default);
}
