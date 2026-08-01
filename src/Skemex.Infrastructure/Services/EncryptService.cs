using Microsoft.AspNetCore.DataProtection;
using Skemex.Application.Services;

namespace Skemex.Infrastructure.Services;

public sealed class EncryptService(IDataProtectionProvider dataProtectionProvider) : IEncryptService
{
    private const string Purpose = "AiProvider.Secrets";

    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(Purpose);

    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        return _protector.Protect(plaintext);
    }

    public string Unprotect(string protectedPayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedPayload);
        return _protector.Unprotect(protectedPayload);
    }
}
