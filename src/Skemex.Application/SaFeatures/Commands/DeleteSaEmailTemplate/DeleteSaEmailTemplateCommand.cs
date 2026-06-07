using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.SaFeatures.Commands.DeleteSaEmailTemplate;

public sealed class DeleteSaEmailTemplateCommand : ICommand
{
    public Guid TemplateId { get; set; }
}
