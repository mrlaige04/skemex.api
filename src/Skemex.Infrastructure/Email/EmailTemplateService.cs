using Microsoft.Extensions.Logging;
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
using Skemex.Domain.Entities.EmailTemplates;
using Skemex.Domain.Enums;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Infrastructure.Email;

public sealed class EmailTemplateService(
    IBaseRepository<EmailTemplate> emailTemplateRepository,
    IEmailSender emailSender,
    ILogger<EmailTemplateService> logger) : IEmailTemplateService
{
    public async Task SendAsync(
        EmailTemplateType type,
        Guid? tenantId,
        string toEmail,
        IReadOnlyDictionary<string, string> placeholders,
        CancellationToken cancellationToken = default)
    {
        var template = await ResolveTemplateAsync(type, tenantId, cancellationToken).ConfigureAwait(false);
        if (template is null)
        {
            throw new InvalidOperationException($"Email template '{type}' was not found.");
        }

        var subject = EmailTemplatePlaceholderRenderer.Render(template.Subject, placeholders);
        var body = EmailTemplatePlaceholderRenderer.Render(template.Body, placeholders);

        try
        {
            await emailSender
                .SendEmailAsync(toEmail, subject, body, isHtml: true, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to send templated email {TemplateType} to {Email}.",
                type,
                toEmail);
            throw;
        }
    }

    private async Task<EmailTemplate?> ResolveTemplateAsync(
        EmailTemplateType type,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        EmailTemplate? template = null;

        if (tenantId is not null)
        {
            template = await emailTemplateRepository.GetAsync(
                t => !t.IsSystem && t.TenantId == tenantId && t.Type == type,
                cancellationToken: cancellationToken);
        }

        template ??= await emailTemplateRepository.GetAsync(
            t => t.IsSystem && t.TenantId == null && t.Type == type,
            cancellationToken: cancellationToken);

        if (template is null)
        {
            logger.LogError("Email template {TemplateType} was not found for tenant {TenantId}.", type, tenantId);
        }

        return template;
    }
}
