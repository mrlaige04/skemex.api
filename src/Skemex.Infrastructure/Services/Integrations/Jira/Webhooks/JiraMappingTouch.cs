using Skemex.Domain.Entities.Integrations;
using Skemex.Domain.Enums;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Infrastructure.Services.Integrations.Jira.Webhooks;

/// <summary>Shared helpers to keep IntegrationMapping rows Active + current after Jira sync.</summary>
internal static class JiraMappingTouch
{
    public static void MarkActive(IntegrationMapping mapping)
    {
        mapping.Status = IntegrationMappingStatus.Active;
        mapping.LastSyncedAt = DateTimeOffset.UtcNow;
    }

    public static async Task EnsureProjectUserMappingAsync(
        ITenantRepository<IntegrationMapping> mappingRepository,
        ProjectIntegration integration,
        Guid projectUserId,
        string externalAccountId,
        string? externalKey,
        object? externalData,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalAccountId) || projectUserId == Guid.Empty)
        {
            return;
        }

        var existing = await mappingRepository.GetAsync(
            filter: m =>
                m.ProjectId == integration.ProjectId
                && m.ProjectIntegrationId == integration.Id
                && m.LocalEntityType == IntegrationLocalEntityTypes.ProjectUser
                && (m.LocalEntityId == projectUserId
                    || m.ExternalEntityId == externalAccountId),
            cancellationToken: cancellationToken);

        if (existing is null)
        {
            await mappingRepository.AddAsync(
                IntegrationMappingFactory.ForProjectUser(
                    integration.TenantId,
                    integration.ProjectId,
                    integration.Id,
                    projectUserId,
                    externalAccountId.Trim(),
                    externalKey,
                    externalData),
                cancellationToken);
            return;
        }

        existing.LocalEntityId = projectUserId;
        existing.ProjectUserId = projectUserId;
        existing.LocalEntityType = IntegrationLocalEntityTypes.ProjectUser;
        existing.ExternalEntityType = IntegrationExternalEntityTypes.User;
        existing.ExternalEntityId = externalAccountId.Trim();
        if (!string.IsNullOrWhiteSpace(externalKey))
        {
            existing.ExternalEntityKey = externalKey;
        }

        existing.ExternalDataJson = IntegrationMappingFactory.SerializeExternalData(externalData)
            ?? existing.ExternalDataJson;
        MarkActive(existing);
        await mappingRepository.UpdateAsync(existing, cancellationToken);
    }
}
