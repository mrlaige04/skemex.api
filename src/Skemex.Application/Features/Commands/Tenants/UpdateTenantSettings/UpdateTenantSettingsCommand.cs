using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Tenants;

namespace Skemex.Application.Features.Commands.Tenants.UpdateTenantSettings;

public sealed class UpdateTenantSettingsCommand : ICommand<TenantSettingsDto>
{
    /// <summary>When set, updates the default AI model for new projects.</summary>
    public Guid? DefaultAiModelId { get; set; }

    /// <summary>When true, clears the workspace default AI model.</summary>
    public bool ClearDefaultAiModel { get; set; }

    /// <summary>When set, updates the document chunk enrichment AI model default.</summary>
    public Guid? EnrichmentAiModelId { get; set; }

    /// <summary>When true, clears the enrichment AI model default.</summary>
    public bool ClearEnrichmentAiModel { get; set; }

    /// <summary>When set, updates the workspace LLM sampling temperature default.</summary>
    public double? AiTemperature { get; set; }
}
