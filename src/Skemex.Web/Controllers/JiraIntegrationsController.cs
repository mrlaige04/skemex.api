using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Features.Commands.Integrations.Jira.DeleteJiraEntityMapping;
using Skemex.Application.Features.Commands.Integrations.Jira.DisconnectJira;
using Skemex.Application.Features.Commands.Integrations.Jira.ExportTasksToJira;
using Skemex.Application.Features.Commands.Integrations.Jira.ImportTasksFromJira;
using Skemex.Application.Features.Commands.Integrations.Jira.MapJiraProject;
using Skemex.Application.Features.Commands.Integrations.Jira.StartJiraOAuth;
using Skemex.Application.Features.Commands.Integrations.Jira.UpdateJiraWebhookSettings;
using Skemex.Application.Features.Commands.Integrations.Jira.UpsertJiraEntityMapping;
using Skemex.Application.Features.Queries.Integrations.Jira.GetJiraConnection;
using Skemex.Application.Features.Queries.Integrations.Jira.GetJiraEntityMappings;
using Skemex.Application.Features.Queries.Integrations.Jira.ListJiraIssues;
using Skemex.Application.Features.Queries.Integrations.Jira.ListJiraProjects;
using Skemex.Application.Features.Queries.Integrations.Jira.ListJiraSites;
using Skemex.Application.Features.Queries.Integrations.Jira.ListJiraUsers;
using Skemex.Application.Models.Integrations.Jira;
using Skemex.Application.Services.Integrations.Jira;
using Skemex.Domain.Consts;
using Skemex.Web.Attributes;

namespace Skemex.Web.Controllers;

[Route("api/projects/{projectId:guid}/integrations/jira")]
[Authorize]
public sealed class JiraIntegrationsController(ISender sender) : BaseController
{
    [Permission(PermissionNames.ProjectsEdit)]
    [HttpGet]
    public async Task<IActionResult> GetConnection(Guid projectId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetJiraConnectionQuery { ProjectId = projectId },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [Permission(PermissionNames.ProjectsEdit)]
    [HttpPost("connect")]
    public async Task<IActionResult> Connect(Guid projectId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new StartJiraOAuthCommand { ProjectId = projectId },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [Permission(PermissionNames.ProjectsEdit)]
    [HttpGet("sites")]
    public async Task<IActionResult> ListSites(Guid projectId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ListJiraSitesQuery { ProjectId = projectId },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [Permission(PermissionNames.ProjectsEdit)]
    [HttpGet("projects")]
    public async Task<IActionResult> ListProjects(
        Guid projectId,
        [FromQuery] string? cloudId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ListJiraProjectsQuery { ProjectId = projectId, CloudId = cloudId },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [Permission(PermissionNames.ProjectsEdit)]
    [HttpPut("mapping")]
    public async Task<IActionResult> MapProject(
        Guid projectId,
        [FromBody] MapJiraProjectRequest body,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new MapJiraProjectCommand
            {
                ProjectId = projectId,
                CloudId = body.CloudId,
                SiteUrl = body.SiteUrl,
                SiteName = body.SiteName,
                ExternalProjectId = body.ExternalProjectId,
                ExternalProjectKey = body.ExternalProjectKey,
                ExternalProjectName = body.ExternalProjectName,
            },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [Permission(PermissionNames.ProjectsEdit)]
    [HttpDelete]
    public async Task<IActionResult> Disconnect(Guid projectId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new DisconnectJiraCommand { ProjectId = projectId },
            cancellationToken);
        return result.Match(_ => NoContent(), Problem);
    }

    [Permission(PermissionNames.ProjectsEdit)]
    [HttpPut("webhook-settings")]
    public async Task<IActionResult> UpdateWebhookSettings(
        Guid projectId,
        [FromBody] UpdateJiraWebhookSettingsRequest body,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateJiraWebhookSettingsCommand
            {
                ProjectId = projectId,
                Events = body.Events ?? new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase),
            },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [Permission(PermissionNames.ProjectsEdit)]
    [HttpPost("export-tasks")]
    public async Task<IActionResult> ExportTasks(
        Guid projectId,
        [FromBody] ExportTasksToJiraRequest body,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ExportTasksToJiraCommand
            {
                ProjectId = projectId,
                Scope = body.Scope,
                SelectedTaskIds = body.SelectedTaskIds ?? [],
                Search = body.Search,
                ColumnId = body.ColumnId,
                AssigneeId = body.AssigneeId,
                Unassigned = body.Unassigned,
                SkipAlreadyExported = body.SkipAlreadyExported,
            },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [Permission(PermissionNames.ProjectsEdit)]
    [HttpGet("issues")]
    public async Task<IActionResult> ListIssues(Guid projectId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ListJiraIssuesQuery { ProjectId = projectId },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [Permission(PermissionNames.ProjectsEdit)]
    [HttpGet("users")]
    public async Task<IActionResult> ListUsers(Guid projectId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ListJiraUsersQuery { ProjectId = projectId },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [Permission(PermissionNames.ProjectsEdit)]
    [HttpPost("import-tasks")]
    public async Task<IActionResult> ImportTasks(
        Guid projectId,
        [FromBody] ImportTasksFromJiraRequest body,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ImportTasksFromJiraCommand
            {
                ProjectId = projectId,
                ImportAll = body.ImportAll,
                IssueKeys = body.IssueKeys ?? [],
                SkipAlreadyImported = body.SkipAlreadyImported,
            },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [Permission(PermissionNames.ProjectsEdit)]
    [HttpGet("entity-mappings")]
    public async Task<IActionResult> GetEntityMappings(
        Guid projectId,
        [FromQuery] string localEntityType,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetJiraEntityMappingsQuery
            {
                ProjectId = projectId,
                LocalEntityType = localEntityType,
            },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [Permission(PermissionNames.ProjectsEdit)]
    [HttpPut("entity-mappings")]
    public async Task<IActionResult> UpsertEntityMapping(
        Guid projectId,
        [FromBody] UpsertJiraEntityMappingRequest body,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpsertJiraEntityMappingCommand
            {
                ProjectId = projectId,
                LocalEntityType = body.LocalEntityType,
                LocalEntityId = body.LocalEntityId,
                ExternalEntityId = body.ExternalEntityId,
                ExternalEntityKey = body.ExternalEntityKey,
                Status = body.Status,
            },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [Permission(PermissionNames.ProjectsEdit)]
    [HttpDelete("entity-mappings/{mappingId:guid}")]
    public async Task<IActionResult> DeleteEntityMapping(
        Guid projectId,
        Guid mappingId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new DeleteJiraEntityMappingCommand
            {
                ProjectId = projectId,
                MappingId = mappingId,
            },
            cancellationToken);
        return result.Match(_ => NoContent(), Problem);
    }
}

[Route("api/integrations/jira")]
public sealed class JiraOAuthCallbackController(
    IJiraOAuthService jiraOAuthService,
    IJiraWebhookService jiraWebhookService) : BaseController
{
    [AllowAnonymous]
    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromQuery] string? error_description,
        CancellationToken cancellationToken)
    {
        var redirect = await jiraOAuthService.HandleCallbackAsync(
            code,
            state,
            error,
            error_description,
            cancellationToken);
        return Redirect(redirect.ToString());
    }

    /// <summary>
    /// Single inbound Jira Cloud webhook for the integration.
    /// Connection is resolved by <c>?token=</c>; <c>webhookEvent</c> in the payload selects the handler.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook(
        [FromQuery] string? token,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        var result = await jiraWebhookService.HandleInboundAsync(token, body, cancellationToken);
        return result.Match(_ => Ok(), Problem);
    }
}
