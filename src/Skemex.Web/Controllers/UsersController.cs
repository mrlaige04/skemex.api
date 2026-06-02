using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Features.TenantUsers.Commands.CreateTenantUser;
using Skemex.Application.Features.TenantUsers.Commands.DeleteTenantUser;
using Skemex.Application.Features.TenantUsers.Commands.UpdateTenantUser;
using Skemex.Application.Features.TenantUsers.Queries.GetTenantRoles;
using Skemex.Application.Features.TenantUsers.Queries.GetTenantUserById;
using Skemex.Application.Features.TenantUsers.Queries.GetTenantUsers;

namespace Skemex.Web.Controllers;

[Route("api/users")]
[Authorize]
public class UsersController(ISender sender) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetTenantUsersQuery { Search = search, PageNumber = page, PageSize = pageSize },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpGet("roles")]
    public async Task<IActionResult> Roles(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTenantRolesQuery(), cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTenantUserByIdQuery { UserId = id }, cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateTenantUserCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Match(dto => CreatedAtAction(nameof(Get), new { id = dto.Id }, dto), Problem);
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Patch(
        Guid id,
        [FromBody] UpdateTenantUserRequest body,
        CancellationToken cancellationToken)
    {
        var command = new UpdateTenantUserCommand
        {
            UserId = id,
            Email = body.Email,
            FirstName = body.FirstName,
            LastName = body.LastName,
            RoleName = body.RoleName,
        };

        var result = await sender.Send(command, cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteTenantUserCommand { UserId = id }, cancellationToken);
        return result.Match(_ => NoContent(), Problem);
    }
}

public sealed class UpdateTenantUserRequest
{
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? RoleName { get; set; }
}
