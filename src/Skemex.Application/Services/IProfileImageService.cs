namespace Skemex.Application.Services;

public interface IProfileImageService
{
    Task<string> CreateAsync(
        Guid userId,
        Stream content,
        string contentType,
        string? fileName,
        CancellationToken cancellationToken = default);

    Task<string> ReplaceAsync(
        Guid userId,
        string? previousStorageKey,
        Stream content,
        string contentType,
        string? fileName,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
