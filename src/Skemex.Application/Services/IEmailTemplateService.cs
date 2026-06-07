using Skemex.Domain.Enums;

namespace Skemex.Application.Services;

public interface IEmailTemplateService
{
    Task SendAsync(
        EmailTemplateType type,
        Guid? tenantId,
        string toEmail,
        IReadOnlyDictionary<string, string> placeholders,
        CancellationToken cancellationToken = default);
}
