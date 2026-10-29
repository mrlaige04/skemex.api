using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Features.Commands.Integrations.Jira.DisconnectJira;
using Skemex.Application.Features.Commands.Integrations.Jira.MapJiraProject;
using Skemex.Application.Features.Commands.Integrations.Jira.StartJiraOAuth;
using Skemex.Application.Features.Queries.Integrations.Jira.GetJiraConnection;
using Skemex.Application.Features.Queries.Integrations.Jira.ListJiraProjects;
using Skemex.Application.Features.Queries.Integrations.Jira.ListJiraSites;
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
}

[Route("api/integrations/jira")]
public sealed class JiraOAuthCallbackController(IJiraOAuthService jiraOAuthService) : BaseController
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
}
