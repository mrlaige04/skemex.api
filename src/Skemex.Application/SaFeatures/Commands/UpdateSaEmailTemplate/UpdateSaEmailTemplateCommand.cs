using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.SaFeatures.Commands.UpdateSaEmailTemplate;

public sealed class UpdateSaEmailTemplateCommand : ICommand<SaEmailTemplateDto>
{
    public Guid TemplateId { get; set; }

    public string? Title { get; set; }

    public string? Subject { get; set; }

    public string? Body { get; set; }
}
