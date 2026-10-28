using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

namespace Skemex.Web.Controllers;

[ApiController]
public sealed class BlobsController(IBlobStorageService storage, IOptions<StorageOptions> options) : ControllerBase
{
    private readonly StorageOptions _options = options.Value;

    [HttpGet("/api/blobs/branding/{**blobPath}")]
    [HttpGet("/blobs/branding/{**blobPath}")]
    [AllowAnonymous]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public Task<IActionResult> GetBranding(string blobPath, CancellationToken cancellationToken) =>
        Get(StorageBucketKind.Branding, blobPath, cancellationToken);

    [HttpGet("/api/blobs/files/{**blobPath}")]
    [HttpGet("/blobs/files/{**blobPath}")]
    [AllowAnonymous]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public Task<IActionResult> GetFiles(string blobPath, CancellationToken cancellationToken) =>
        Get(StorageBucketKind.Files, blobPath, cancellationToken);

    [HttpGet("/api/blobs/project-documents/{**blobPath}")]
    [HttpGet("/blobs/project-documents/{**blobPath}")]
    [AllowAnonymous]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public Task<IActionResult> GetProjectDocuments(string blobPath, CancellationToken cancellationToken) =>
        Get(StorageBucketKind.ProjectDocuments, blobPath, cancellationToken);

    [HttpGet("/api/blobs/issues-attachments/{**blobPath}")]
    [HttpGet("/blobs/issues-attachments/{**blobPath}")]
    [AllowAnonymous]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public Task<IActionResult> GetIssueAttachments(string blobPath, CancellationToken cancellationToken) =>
        Get(StorageBucketKind.IssueAttachments, blobPath, cancellationToken);

    [HttpGet("/api/blobs/ai-chat-attachments/{**blobPath}")]
    [HttpGet("/blobs/ai-chat-attachments/{**blobPath}")]
    [AllowAnonymous]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public Task<IActionResult> GetAiChatAttachments(string blobPath, CancellationToken cancellationToken) =>
        Get(StorageBucketKind.AiChatAttachments, blobPath, cancellationToken);

    [HttpGet("/api/blobs/{**blobPath}")]
    [HttpGet("/blobs/{**blobPath}")]
    [AllowAnonymous]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public Task<IActionResult> GetLegacy(string blobPath, CancellationToken cancellationToken) =>
        Get(StorageBucketKind.Branding, blobPath, cancellationToken);

    private async Task<IActionResult> Get(StorageBucketKind bucket, string blobPath, CancellationToken cancellationToken)
    {
        if (string.Equals(_options.Provider, StorageProviderNames.Production, StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(blobPath))
        {
            return NotFound();
        }

        var bucketName = StorageBucketNames.Resolve(_options, bucket);

        try
        {
            var (stream, contentType) =
                await storage.DownloadAsync(bucketName, blobPath, cancellationToken).ConfigureAwait(false);
            return File(stream, contentType, enableRangeProcessing: true);
        }
        catch (ArgumentException)
        {
            return BadRequest();
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }
}
