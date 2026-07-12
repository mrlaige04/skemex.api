using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Features.Commands.Users.CreateTenantUser;
using Skemex.Application.Features.Commands.Users.DeleteTenantUser;
using Skemex.Application.Features.Commands.Users.UpdateTenantUser;
using Skemex.Application.Features.Queries.Users.GetTenantRoles;
using Skemex.Application.Features.Queries.Users.GetTenantUserById;
using Skemex.Application.Features.Queries.Users.GetTenantUsers;
using Skemex.Application.Features.Queries.Users.LookupUserByEmail;
using Skemex.Web.Models.Users;

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

    [HttpGet("lookup")]
    public async Task<IActionResult> LookupByEmail(
        [FromQuery] string email,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new LookupUserByEmailQuery { Email = email }, cancellationToken);
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
