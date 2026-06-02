using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skemex.Application.Features.Abstractions;
using Skemex.Infrastructure.Tenants.CreateTenant;

namespace Skemex.Web.Controllers;

[Route("api/tenants")]
[Authorize]
public class TenantsController(ISender sender) : BaseController
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTenantCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Match(Ok, Problem);
    }
}
