using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaFeatures.Commands.CreateSaUser;
using Skemex.Application.SaFeatures.Commands.DeleteSaUser;
using Skemex.Application.SaFeatures.Commands.UpdateSaUser;
using Skemex.Application.SaFeatures.Queries.GetSaUserById;
using Skemex.Application.SaFeatures.Queries.GetSaUsers;
using Skemex.Web.Attributes;

namespace Skemex.Web.Controllers;

[Route("api/sa/users")]
[Authorize]
[OnlySuperAdmin]
public class SaUsersController(ISender sender) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetSaUsersQuery { Search = search, PageNumber = page, PageSize = pageSize },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetSaUserByIdQuery { UserId = id }, cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateSaUserCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Match(dto => CreatedAtAction(nameof(Get), new { id = dto.Id }, dto), Problem);
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Patch(
        Guid id,
        [FromBody] UpdateSaUserRequest body,
        CancellationToken cancellationToken)
    {
        var command = new UpdateSaUserCommand
        {
            UserId = id,
            Email = body.Email,
            FirstName = body.FirstName,
            LastName = body.LastName,
        };

        var result = await sender.Send(command, cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteSaUserCommand { UserId = id }, cancellationToken);
        return result.Match(_ => NoContent(), Problem);
    }
}

public sealed class UpdateSaUserRequest
{
    public string? Email { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }
}
