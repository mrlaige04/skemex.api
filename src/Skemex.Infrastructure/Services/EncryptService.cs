using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration.App;
using Skemex.Application.Services.Encryption;

namespace Skemex.Infrastructure.Services;

/// <summary>
/// AES-256-GCM encryption with a master key from configuration (env / GitHub secrets).
/// Survives redeploys without a Data Protection key-ring on disk.
/// </summary>
public sealed class EncryptService : IEncryptService
{
    private const string PayloadPrefix = "skemex-v1.";
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;

    private readonly byte[] _key;

    public EncryptService(IOptions<EncryptionOptions> options)
    {
        _key = DecodeMasterKey(options.Value.MasterKey);
    }

    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintextBytes, cipher, tag);

        var payload = new byte[NonceSize + cipher.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, payload, 0, NonceSize);
        Buffer.BlockCopy(cipher, 0, payload, NonceSize, cipher.Length);
        Buffer.BlockCopy(tag, 0, payload, NonceSize + cipher.Length, TagSize);

        return PayloadPrefix + Convert.ToBase64String(payload);
    }

    public string Unprotect(string protectedPayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedPayload);

        if (!protectedPayload.StartsWith(PayloadPrefix, StringComparison.Ordinal))
        {
            throw new CryptographicException(
                "Secret uses an outdated encryption format (ASP.NET Data Protection). " +
                "Re-save the AI provider API key / auth values after setting Encryption__MasterKey.");
        }

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(protectedPayload[PayloadPrefix.Length..]);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("Encrypted secret payload is not valid Base64.", ex);
        }

        if (payload.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("Encrypted secret payload is truncated.");
        }

        var nonce = payload.AsSpan(0, NonceSize);
        var tag = payload.AsSpan(payload.Length - TagSize, TagSize);
        var cipher = payload.AsSpan(NonceSize, payload.Length - NonceSize - TagSize);
        var plaintextBytes = new byte[cipher.Length];

        try
        {
            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, cipher, tag, plaintextBytes);
        }
        catch (CryptographicException ex)
        {
            throw new CryptographicException(
                "Failed to decrypt secret. Check that Encryption__MasterKey matches the key used when the secret was saved.",
                ex);
        }

        return Encoding.UTF8.GetString(plaintextBytes);
    }

    private static byte[] DecodeMasterKey(string? masterKey)
    {
        if (string.IsNullOrWhiteSpace(masterKey))
        {
            throw new InvalidOperationException(
                "Encryption:MasterKey is not configured. " +
                "Set Encryption__MasterKey to a Base64 32-byte key (e.g. from GitHub Actions secrets).");
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(masterKey.Trim());
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "Encryption:MasterKey must be Base64-encoded.",
                ex);
        }

        if (key.Length != KeySize)
        {
            throw new InvalidOperationException(
                $"Encryption:MasterKey must decode to exactly {KeySize} bytes (AES-256), got {key.Length}.");
        }

        return key;
    }
}
