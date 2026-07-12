using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Entities.EmailTemplates;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;
using Skemex.Application.SaModels.SaEmailTemplates;

namespace Skemex.Application.SaFeatures.Commands.SaEmailTemplates.UpdateSaEmailTemplate;

public sealed class UpdateSaEmailTemplateCommandHandler(
    ICurrentUser currentUser,
    IBaseRepository<EmailTemplate> emailTemplateRepository,
    IBaseRepository<Tenant> tenantRepository)
    : ICommandHandler<UpdateSaEmailTemplateCommand, SaEmailTemplateDto>
{
    public async Task<ErrorOr<SaEmailTemplateDto>> Handle(
        UpdateSaEmailTemplateCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsSuperAdmin())
        {
            return Error.Forbidden("SuperAdmin.Required", "Platform administrator access is required.");
        }

        var template = await emailTemplateRepository.GetAsync(
            filter: t => t.Id == request.TemplateId,
            cancellationToken: cancellationToken);

        if (template is null)
        {
            return Error.NotFound("EmailTemplate.NotFound", "Email template was not found.");
        }

        var changed = false;

        if (request.Title is not null)
        {
            var title = request.Title.Trim();
            if (title.Length > 0 && !string.Equals(title, template.Title, StringComparison.Ordinal))
            {
                template.Title = title;
                changed = true;
            }
        }

        if (request.Subject is not null)
        {
            var subject = request.Subject.Trim();
            if (subject.Length > 0 && !string.Equals(subject, template.Subject, StringComparison.Ordinal))
            {
                template.Subject = subject;
                changed = true;
            }
        }

        if (request.Body is not null)
        {
            var body = request.Body.Trim();
            if (body.Length > 0 && !string.Equals(body, template.Body, StringComparison.Ordinal))
            {
                template.Body = body;
                changed = true;
            }
        }

        if (changed)
        {
            await emailTemplateRepository.UpdateAsync(template, cancellationToken);
        }

        string? tenantName = null;
        if (template.TenantId is Guid tenantId)
        {
            var tenant = await tenantRepository.GetAsync(
                filter: t => t.Id == tenantId,
                cancellationToken: cancellationToken);
            tenantName = tenant?.Name;
        }

        return new SaEmailTemplateDto
        {
            Id = template.Id,
            Title = template.Title,
            Type = template.Type.ToString(),
            Subject = template.Subject,
            Body = template.Body,
            IsSystem = template.IsSystem,
            TenantId = template.TenantId,
            TenantName = tenantName,
            Scope = ResolveScope(template, tenantName),
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt,
        };
    }

    private static string ResolveScope(EmailTemplate template, string? tenantName)
    {
        if (template.TenantId is not null)
        {
            return string.IsNullOrWhiteSpace(tenantName) ? "Company" : tenantName.Trim();
        }

        return template.IsSystem ? "System" : "Company";
    }
}
