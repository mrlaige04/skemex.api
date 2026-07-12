using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Entities.EmailTemplates;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;
using Skemex.Application.SaModels.SaEmailTemplates;

namespace Skemex.Application.SaFeatures.Queries.SaEmailTemplates.GetSaEmailTemplateById;

public sealed class GetSaEmailTemplateByIdQueryHandler(
    ICurrentUser currentUser,
    IBaseRepository<EmailTemplate> emailTemplateRepository,
    IBaseRepository<Tenant> tenantRepository)
    : IQueryHandler<GetSaEmailTemplateByIdQuery, SaEmailTemplateDto>
{
    public async Task<ErrorOr<SaEmailTemplateDto>> Handle(
        GetSaEmailTemplateByIdQuery request,
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

