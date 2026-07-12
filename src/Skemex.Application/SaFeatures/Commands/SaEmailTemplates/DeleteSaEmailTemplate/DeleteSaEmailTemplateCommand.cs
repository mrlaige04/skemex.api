using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.SaFeatures.Commands.SaEmailTemplates.DeleteSaEmailTemplate;

public sealed class DeleteSaEmailTemplateCommand : ICommand
{
    public Guid TemplateId { get; set; }
}
