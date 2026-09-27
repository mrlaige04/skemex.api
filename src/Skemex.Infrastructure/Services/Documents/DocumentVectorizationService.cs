using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;
using Skemex.Application.Configuration;
using Skemex.Application.Services;
using Skemex.Application.Services.Documents;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Infrastructure.Services.Documents;

public sealed class DocumentVectorizationService(
    ITenantRepository<ProjectDocument> documentRepository,
    ITenantRepository<ProjectDocumentChunk> chunkRepository,
    ITenantRepository<ProjectSettings> projectSettingsRepository,
    IProjectDocumentStorageService documentStorage,
    IDocumentTextExtractor textExtractor,
    ITextChunker textChunker,
    IDocumentChunkEnrichmentService chunkEnrichmentService,
    IEmbeddingService embeddingService,
    IOptions<DocumentIngestionOptions> ingestionOptions,
    ILogger<DocumentVectorizationService> logger) : IDocumentVectorizationService
{
    public string Enqueue(Guid documentId, Guid projectId, Guid tenantId, Guid requestedByUserId) =>
        BackgroundJob.Enqueue<DocumentVectorizationService>(service =>
            service.ProcessDocumentAsync(
                documentId,
                projectId,
                tenantId,
                requestedByUserId,
                CancellationToken.None));

    [AutomaticRetry(Attempts = 2)]
    public async Task ProcessDocumentAsync(
        Guid documentId,
        Guid projectId,
        Guid tenantId,
        Guid requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        using var ambient = AmbientUserContext.Use(tenantId, requestedByUserId);

        var document = await documentRepository.GetAsync(
            filter: entry => entry.Id == documentId && entry.ProjectId == projectId,
            cancellationToken: cancellationToken);

        if (document is null)
        {
            logger.LogWarning(
                "Document vectorization skipped because document {DocumentId} was not found.",
                documentId);
            return;
        }

        if (document.VectorizationStatus is ProjectDocumentVectorizationStatus.Ready
            or ProjectDocumentVectorizationStatus.Skipped)
        {
            logger.LogInformation(
                "Document {DocumentId} vectorization already finished with status {Status}; skipping.",
                documentId,
                document.VectorizationStatus);
            return;
        }

        document.VectorizationStatus = ProjectDocumentVectorizationStatus.Processing;
        document.VectorizationError = null;
        document.UpdatedAt = DateTime.UtcNow;
        await documentRepository.UpdateAsync(document, cancellationToken);

        try
        {
            if (!textExtractor.CanExtract(document.FileName, document.ContentType))
            {
                await MarkSkippedAsync(
                    document,
                    "Vectorization applies to PDF and DOCX files only.",
                    cancellationToken);
                return;
            }

            await using var stream = await documentStorage
                .OpenReadAsync(document.BlobId, cancellationToken)
                .ConfigureAwait(false);

            var extractedText = await textExtractor
                .ExtractAsync(stream, document.FileName, document.ContentType, cancellationToken)
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                await MarkFailedAsync(
                    document,
                    "No extractable text was found in the document.",
                    cancellationToken);
                return;
            }

            var chunks = textChunker.Chunk(extractedText);
            if (chunks.Count == 0)
            {
                await MarkFailedAsync(
                    document,
                    "Document text could not be split into chunks.",
                    cancellationToken);
                return;
            }

            var enrichmentModel = await ResolveEnrichmentModelAsync(projectId, cancellationToken)
                .ConfigureAwait(false);
            var enriched = await chunkEnrichmentService
                .EnrichAsync(chunks, enrichmentModel, cancellationToken)
                .ConfigureAwait(false);

            var embedInputs = enriched
                .Select(BuildEmbeddingInput)
                .ToList();

            var embeddings = await embeddingService
                .EmbedBatchAsync(embedInputs, cancellationToken)
                .ConfigureAwait(false);

            await ReplaceChunksAsync(document, enriched, embeddings, cancellationToken)
                .ConfigureAwait(false);

            document.VectorizationStatus = ProjectDocumentVectorizationStatus.Ready;
            document.VectorizationError = null;
            document.VectorizedChunkCount = enriched.Count;
            document.UpdatedAt = DateTime.UtcNow;
            await documentRepository.UpdateAsync(document, cancellationToken);

            logger.LogInformation(
                "Vectorized document {DocumentId} into {ChunkCount} enriched chunks.",
                documentId,
                enriched.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Document vectorization failed for {DocumentId}.", documentId);
            await MarkFailedAsync(
                document,
                ex.Message,
                cancellationToken);
        }
    }

    private async Task<string> ResolveEnrichmentModelAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var configured = ingestionOptions.Value.EnrichmentModel?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var settings = await projectSettingsRepository.GetAsync(
            filter: entry => entry.ProjectId == projectId,
            include: query => query.Include(entry => entry.DefaultAiModel),
            cancellationToken: cancellationToken);

        var fromProject = settings?.DefaultAiModel?.ExternalId?.Trim();
        if (!string.IsNullOrWhiteSpace(fromProject))
        {
            return fromProject;
        }

        throw new InvalidOperationException(
            "No enrichment model configured. Set DocumentIngestion:EnrichmentModel in appsettings, " +
            "or configure a project default AI model.");
    }

    private static string BuildEmbeddingInput(EnrichedChunk chunk)
    {
        var keywords = string.Join(", ", chunk.Keywords);
        var content = chunk.EnglishText ?? chunk.Text;
        return $"Keywords: {keywords}\nContent: {content}";
    }

    private async Task ReplaceChunksAsync(
        ProjectDocument document,
        IReadOnlyList<EnrichedChunk> chunks,
        IReadOnlyList<float[]> embeddings,
        CancellationToken cancellationToken)
    {
        var existingChunks = await chunkRepository.GetAllAsync(
            filter: chunk => chunk.DocumentId == document.Id,
            cancellationToken: cancellationToken);

        if (existingChunks.Count > 0)
        {
            await chunkRepository.DeleteRangeAsync(existingChunks, cancellationToken);
        }

        var entities = chunks
            .Select((chunk, index) => new ProjectDocumentChunk
            {
                Id = Guid.NewGuid(),
                TenantId = document.TenantId,
                ProjectId = document.ProjectId,
                DocumentId = document.Id,
                ChunkIndex = index,
                Text = chunk.Text,
                EnglishText = chunk.EnglishText,
                Keywords = chunk.Keywords
                    .Select(k => k.Trim().ToLowerInvariant())
                    .Where(k => k.Length > 0)
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
                Embedding = new Vector(embeddings[index]),
            })
            .ToList();

        await chunkRepository.AddRangeAsync(entities, cancellationToken);
    }

    private async Task MarkSkippedAsync(
        ProjectDocument document,
        string? reason,
        CancellationToken cancellationToken)
    {
        document.VectorizationStatus = ProjectDocumentVectorizationStatus.Skipped;
        document.VectorizationError = reason is null ? null : Truncate(reason, 2000);
        document.VectorizedChunkCount = null;
        document.UpdatedAt = DateTime.UtcNow;
        await documentRepository.UpdateAsync(document, cancellationToken);
    }

    private async Task MarkFailedAsync(
        ProjectDocument document,
        string error,
        CancellationToken cancellationToken)
    {
        document.VectorizationStatus = ProjectDocumentVectorizationStatus.Failed;
        document.VectorizationError = Truncate(error, 2000);
        document.VectorizedChunkCount = null;
        document.UpdatedAt = DateTime.UtcNow;
        await documentRepository.UpdateAsync(document, cancellationToken);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
