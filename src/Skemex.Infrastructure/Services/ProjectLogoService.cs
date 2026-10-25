using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Configuration.Ai;
using Skemex.Application.Configuration.App;
using Skemex.Application.Configuration.Documents;
using Skemex.Application.Configuration.Email;
using Skemex.Application.Configuration.Rag;
using Skemex.Application.Configuration.Storage;
using Skemex.Application.Services.Ai.Agents;
using Skemex.Application.Services.Ai.Chat;
using Skemex.Application.Services.Ai.Completions;
using Skemex.Application.Services.Ai.Embeddings;
using Skemex.Application.Services.Ai.Providers;
using Skemex.Application.Services.Ai.Rag;
using Skemex.Application.Services.Ai.Usage;
using Skemex.Application.Services.Common;
using Skemex.Application.Services.Documents;
using Skemex.Application.Services.Email;
using Skemex.Application.Services.Encryption;
using Skemex.Application.Services.Notifications;
using Skemex.Application.Services.Projects;
using Skemex.Application.Services.Storage;
using Skemex.Application.Services.Users;
using Skemex.Application.Models.Storage;

namespace Skemex.Infrastructure.Services;

public sealed class ProjectLogoService(
    IBlobStorageService blobs,
    IOptions<StorageOptions> storageOptions) : IProjectLogoService
{
    private readonly string _bucket = StorageBucketNames.Resolve(storageOptions.Value, StorageBucketKind.Branding);

    public async Task<string> CreateAsync(
        Guid tenantId,
        Guid projectId,
        Stream content,
        string contentType,
        string? fileName,
        CancellationToken cancellationToken = default)
    {
        await blobs.EnsureBucketExistsAsync(_bucket, cancellationToken).ConfigureAwait(false);

        var storageKey = BuildStorageKey(tenantId, projectId, fileName, contentType);
        content.Position = 0;
        await blobs.UploadAsync(_bucket, storageKey, content, contentType, cancellationToken)
            .ConfigureAwait(false);
        return storageKey;
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return;
        }

        await blobs.EnsureBucketExistsAsync(_bucket, cancellationToken).ConfigureAwait(false);
        await blobs.DeleteAsync(_bucket, storageKey, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildStorageKey(Guid tenantId, Guid projectId, string? fileName, string? contentType)
    {
        var ext = NormalizeExtension(fileName, contentType);
        return $"projects/{tenantId:N}/{projectId:N}/logo-{Guid.NewGuid():N}{ext}";
    }

    private static string NormalizeExtension(string? fileName, string? contentType)
    {
        var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif")
        {
            return ext;
        }

        return contentType?.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".jpg",
        };
    }
}
