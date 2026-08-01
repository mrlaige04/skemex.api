using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaFeatures.Commands.SaAiProviders.CreateSaAiProvider;
using Skemex.Application.SaFeatures.Commands.SaAiProviders.DeleteSaAiProvider;
using Skemex.Application.SaFeatures.Commands.SaAiProviders.UpdateSaAiProvider;
using Skemex.Application.SaFeatures.Commands.SaAiProviders.UpdateSaAiProviderModel;
using Skemex.Application.SaFeatures.Queries.SaAiProviders.GetSaAiProviderById;
using Skemex.Application.SaFeatures.Queries.SaAiProviders.GetSaAiProviderModels;
using Skemex.Application.SaFeatures.Queries.SaAiProviders.GetSaAiProviders;
using Skemex.Web.Attributes;
using Skemex.Web.Models.SuperAdmin;

namespace Skemex.Web.Controllers;

[Route("api/sa/ai-providers")]
[Authorize]
[OnlySuperAdmin]
public class SaAiProvidersController(ISender sender) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetSaAiProvidersQuery(), cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetSaAiProviderByIdQuery { ProviderId = id },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpGet("{id:guid}/models")]
    public async Task<IActionResult> ListModels(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetSaAiProviderModelsQuery { ProviderId = id },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpPatch("{id:guid}/models/{modelId:guid}")]
    public async Task<IActionResult> UpdateModel(
        Guid id,
        Guid modelId,
        [FromBody] UpdateSaAiProviderModelRequest body,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateSaAiProviderModelCommand
            {
                ProviderId = id,
                ModelId = modelId,
                DisplayName = body.DisplayName,
                IsActive = body.IsActive,
            },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateSaAiProviderCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Match(dto => CreatedAtAction(nameof(Get), new { id = dto.Id }, dto), Problem);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateSaAiProviderRequest body,
        CancellationToken cancellationToken)
    {
        var command = new UpdateSaAiProviderCommand
        {
            ProviderId = id,
            Name = body.Name,
            BaseUrl = body.BaseUrl,
            ApiKey = body.ApiKey,
            IsEnabled = body.IsEnabled,
            AuthEntries = body.AuthEntries ?? [],
        };

        var result = await sender.Send(command, cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new DeleteSaAiProviderCommand { ProviderId = id },
            cancellationToken);
        return result.Match(_ => NoContent(), Problem);
    }
}
