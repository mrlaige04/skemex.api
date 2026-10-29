using Skemex.Application.Models.Integrations.Jira;
using Skemex.Domain.Entities.Integrations;

namespace Skemex.Application.Helpers.Integrations;

public static class JiraConnectionDtoHelper
{
    public static JiraConnectionDto Map(IntegrationConnection connection) =>
        new()
        {
            Id = connection.Id,
            ProjectId = connection.ProjectId,
            Status = connection.Status.ToString(),
            CloudId = connection.CloudId,
            SiteUrl = connection.SiteUrl,
            SiteName = connection.SiteName,
            ExternalProjectId = connection.ExternalProjectId,
            ExternalProjectKey = connection.ExternalProjectKey,
            ExternalProjectName = connection.ExternalProjectName,
            LastError = connection.LastError,
            CreatedAt = connection.CreatedAt,
            UpdatedAt = connection.UpdatedAt,
        };
}
