using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaModels.SaEmailTemplates;



namespace Skemex.Application.SaFeatures.Queries.SaEmailTemplates.GetSaEmailTemplates;



public sealed class GetSaEmailTemplatesQuery : IQuery<IReadOnlyList<SaEmailTemplateSummaryDto>>

{

    public string? Search { get; set; }

}


