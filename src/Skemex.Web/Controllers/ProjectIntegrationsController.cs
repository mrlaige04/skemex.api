using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Features.Queries.Integrations.GetIntegrationStatus;
using Skemex.Domain.Consts;
using Skemex.Web.Attributes;

namespace Skemex.Web.Controllers;

/// <summary>Provider-agnostic integration endpoints for a project.</summary>
[Route("api/projects/{projectId:guid}/integrations")]
[Authorize]
public sealed class ProjectIntegrationsController(ISender sender) : BaseController
{
    /// <summary>
    /// Returns whether the given integration provider is configured for the project.
    /// Example: <c>GET .../integrations/status?provider=jira</c>
    /// </summary>
    [Permission(PermissionNames.ProjectsView)]
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(
        Guid projectId,
        [FromQuery] string provider,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetIntegrationStatusQuery
            {
                ProjectId = projectId,
                Provider = provider,
            },
            cancellationToken);
        return result.Match(Ok, Problem);
    }
}
