namespace Skemex.Application.Services;

public interface IDocumentVectorizationService
{
    string Enqueue(Guid documentId, Guid projectId, Guid tenantId, Guid requestedByUserId);

    Task ProcessDocumentAsync(
        Guid documentId,
        Guid projectId,
        Guid tenantId,
        Guid requestedByUserId,
        CancellationToken cancellationToken = default);
}
