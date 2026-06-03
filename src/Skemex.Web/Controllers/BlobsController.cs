using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Services;

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
