using Microsoft.Extensions.Configuration;
using Minio;
using Skemex.Application.Configuration;

namespace Skemex.Infrastructure.Storage;

internal static class SkemexMinioClientFactory
{
    public static IMinioClient Create(StorageOptions storage, IConfiguration configuration, string endpoint)
    {
        var opts = storage.Minio;
        var accessKey = string.IsNullOrWhiteSpace(opts.AccessKey)
            ? configuration["MINIO_ROOT_USER"]
            : opts.AccessKey;

        var secretKey = string.IsNullOrWhiteSpace(opts.SecretKey)
            ? configuration["MINIO_ROOT_PASSWORD"]
            : opts.SecretKey;

        if (string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException(
                "Set Storage:Minio:AccessKey/SecretKey or MINIO_ROOT_USER/MINIO_ROOT_PASSWORD.");
        }

        var (hostPort, useSsl) = MinioEndpoint.Parse(endpoint);

        return new MinioClient()
            .WithEndpoint(hostPort)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(useSsl)
            .Build();
    }
}
