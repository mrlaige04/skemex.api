namespace Skemex.Application.Services;

/// <summary>Encrypts secrets at rest (API keys, auth values).</summary>
public interface IEncryptService
{
    string Protect(string plaintext);

    string Unprotect(string protectedPayload);
}
