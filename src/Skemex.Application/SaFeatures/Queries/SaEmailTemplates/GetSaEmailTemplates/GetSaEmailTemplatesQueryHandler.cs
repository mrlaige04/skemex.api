using System.Linq.Expressions;
using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Entities.EmailTemplates;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;
using Skemex.Application.SaModels.SaEmailTemplates;

namespace Skemex.Application.SaFeatures.Queries.SaEmailTemplates.GetSaEmailTemplates;

public sealed class GetSaEmailTemplatesQueryHandler(
    ICurrentUser currentUser,
    IBaseRepository<EmailTemplate> emailTemplateRepository,
    IBaseRepository<Tenant> tenantRepository)
    : IQueryHandler<GetSaEmailTemplatesQuery, IReadOnlyList<SaEmailTemplateSummaryDto>>
{
    public async Task<ErrorOr<IReadOnlyList<SaEmailTemplateSummaryDto>>> Handle(
        GetSaEmailTemplatesQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsSuperAdmin())
        {
            return Error.Forbidden("SuperAdmin.Required", "Platform administrator access is required.");
        }

        var term = request.Search?.Trim().ToLowerInvariant() ?? string.Empty;
        var hasSearch = term.Length > 0;

        HashSet<Guid>? matchingTenantIds = null;
        if (hasSearch)
        {
            var tenantIds = await tenantRepository.GetAllWithSelectorAsync(
                selector: t => t.Id,
                filter: t => t.Name.ToLower().Contains(term),
                cancellationToken: cancellationToken);

            matchingTenantIds = tenantIds.ToHashSet();
        }

        Expression<Func<EmailTemplate, bool>> filter = t =>
            !hasSearch ||
            t.Title.ToLower().Contains(term) ||
            t.Subject.ToLower().Contains(term) ||
            (t.TenantId != null && matchingTenantIds != null && matchingTenantIds.Contains(t.TenantId.Value));

        var templates = await emailTemplateRepository.GetAllAsync(
            filter: filter,
            include: q => q.OrderBy(t => t.IsSystem ? 0 : 1).ThenBy(t => t.Type).ThenBy(t => t.Title),
            cancellationToken: cancellationToken);

        var tenantIdsOnPage = templates
            .Where(t => t.TenantId.HasValue)
            .Select(t => t.TenantId!.Value)
            .Distinct()
            .ToList();

        var tenantNames = tenantIdsOnPage.Count == 0
            ? new Dictionary<Guid, string>()
            : (await tenantRepository.GetAllWithSelectorAsync(
                selector: t => new { t.Id, t.Name },
                filter: t => tenantIdsOnPage.Contains(t.Id),
                cancellationToken: cancellationToken))
            .ToDictionary(t => t.Id, t => t.Name);

        return templates.Select(t => ToSummary(t, tenantNames)).ToList();
    }

    private static SaEmailTemplateSummaryDto ToSummary(
        EmailTemplate template,
        IReadOnlyDictionary<Guid, string> tenantNames)
    {
        string? tenantName = null;
        if (template.TenantId is Guid tenantId && tenantNames.TryGetValue(tenantId, out var name))
        {
            tenantName = name;
        }

        return new SaEmailTemplateSummaryDto
        {
            Id = template.Id,
            Title = template.Title,
            Type = template.Type.ToString(),
            Subject = template.Subject,
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
