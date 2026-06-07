using Skemex.Domain.Entities.EmailTemplates;
using Skemex.Domain.Enums;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Infrastructure.Email;

public sealed class EmailTemplateSeeder(
    IBaseRepository<EmailTemplate> emailTemplateRepository,
    EmailTemplateFileLoader fileLoader)
{
    public async Task SeedSystemTemplatesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var type in Enum.GetValues<EmailTemplateType>())
        {
            var exists = await emailTemplateRepository.ExistsAsync(
                t => t.IsSystem && t.TenantId == null && t.Type == type,
                cancellationToken: cancellationToken);

            if (exists)
            {
                continue;
            }

            var body = await fileLoader.LoadBodyAsync(type, cancellationToken).ConfigureAwait(false);

            await emailTemplateRepository.AddAsync(
                new EmailTemplate
                {
                    Id = Guid.NewGuid(),
                    IsSystem = true,
                    TenantId = null,
                    Title = EmailTemplateDefaults.DefaultTitle(type),
                    Type = type,
                    Subject = EmailTemplateDefaults.DefaultSubject(type),
                    Body = body.Trim(),
                },
                cancellationToken);
        }
    }
}
