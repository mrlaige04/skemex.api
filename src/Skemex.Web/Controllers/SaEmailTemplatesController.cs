using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaFeatures.Commands.SaEmailTemplates.DeleteSaEmailTemplate;
using Skemex.Application.SaFeatures.Commands.SaEmailTemplates.UpdateSaEmailTemplate;
using Skemex.Application.SaFeatures.Queries.SaEmailTemplates.GetSaEmailTemplateById;
using Skemex.Application.SaFeatures.Queries.SaEmailTemplates.GetSaEmailTemplates;
using Skemex.Web.Attributes;
using Skemex.Web.Models.SuperAdmin;

namespace Skemex.Web.Controllers;

[Route("api/sa/email-templates")]
[Authorize]
[OnlySuperAdmin]
public class SaEmailTemplatesController(ISender sender) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetSaEmailTemplatesQuery { Search = search },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetSaEmailTemplateByIdQuery { TemplateId = id }, cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Patch(
        Guid id,
        [FromBody] UpdateSaEmailTemplateRequest body,
        CancellationToken cancellationToken)
    {
        var command = new UpdateSaEmailTemplateCommand
        {
            TemplateId = id,
            Title = body.Title,
            Subject = body.Subject,
            Body = body.Body,
        };

        var result = await sender.Send(command, cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteSaEmailTemplateCommand { TemplateId = id }, cancellationToken);
        return result.Match(_ => NoContent(), Problem);
    }
}
