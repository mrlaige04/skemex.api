using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.Features.Commands.Projects.DeleteProjectDocument;

public sealed class DeleteProjectDocumentCommand : ICommand
{
    public Guid ProjectId { get; init; }
    public Guid DocumentId { get; init; }
}
