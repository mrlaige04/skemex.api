using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Skemex.Application.Configuration;
using Skemex.Application.Models.Rag;
using Skemex.Application.Services;
using Skemex.Application.Services.Ai;
using Skemex.Infrastructure.Data;

namespace Skemex.Infrastructure.Services.Ai;

public sealed class ProjectRagContextService(
    SkemexDbContext dbContext,
    IEmbeddingService embeddingService,
    IOptions<RagSettings> ragSettings,
    ILogger<ProjectRagContextService> logger) : IProjectRagContextService
{
    public Task<string?> BuildContextAsync(
        Guid projectId,
        string query,
        CancellationToken cancellationToken = default)
        => BuildContextAsync(
            projectId,
            new RagSearchRequest { RefinedQueryEn = query, Keywords = [] },
            cancellationToken);

    public Task<string?> BuildContextAsync(
        Guid projectId,
        IReadOnlyList<string> queries,
        CancellationToken cancellationToken = default)
    {
        var primary = queries?
            .Select(q => q?.Trim() ?? string.Empty)
            .FirstOrDefault(q => q.Length > 0) ?? string.Empty;
        return BuildContextAsync(
            projectId,
            new RagSearchRequest { RefinedQueryEn = primary, Keywords = [] },
            cancellationToken);
    }

    public async Task<string?> BuildContextAsync(
        Guid projectId,
        RagSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var matches = await SearchChunksAsync(projectId, request, cancellationToken)
            .ConfigureAwait(false);
        if (matches.Count == 0)
        {
            return null;
        }

        var maxChunkChars = Math.Clamp(ragSettings.Value.MaxChunkChars, 200, 8000);
        return FormatContext(matches, maxChunkChars);
    }

    public Task<IReadOnlyList<RagChunkMatch>> SearchChunksAsync(
        Guid projectId,
        string query,
        CancellationToken cancellationToken = default)
        => SearchChunksAsync(
            projectId,
            new RagSearchRequest { RefinedQueryEn = query, Keywords = [] },
            cancellationToken);

    public Task<IReadOnlyList<RagChunkMatch>> SearchChunksAsync(
        Guid projectId,
        IReadOnlyList<string> queries,
        CancellationToken cancellationToken = default)
    {
        var primary = queries?
            .Select(q => q?.Trim() ?? string.Empty)
            .FirstOrDefault(q => q.Length > 0) ?? string.Empty;
        return SearchChunksAsync(
            projectId,
            new RagSearchRequest { RefinedQueryEn = primary, Keywords = [] },
            cancellationToken);
    }

    public async Task<IReadOnlyList<RagChunkMatch>> SearchChunksAsync(
        Guid projectId,
        RagSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty || request is null)
        {
            return [];
        }

        var refinedQuery = request.RefinedQueryEn?.Trim() ?? string.Empty;
        var keywords = NormalizeKeywords(request.Keywords);
        if (refinedQuery.Length == 0 && keywords.Count == 0)
        {
            return [];
        }

        try
        {
            var hasChunks = await dbContext.ProjectDocumentChunks
                .AsNoTracking()
                .AnyAsync(chunk => chunk.ProjectId == projectId, cancellationToken)
                .ConfigureAwait(false);
            if (!hasChunks)
            {
                return [];
            }

            var settings = ragSettings.Value;
            var topK = Math.Clamp(settings.TopK, 1, 32);
            var rrfK = Math.Max(1, settings.RrfConstant);

            var vectorTask = refinedQuery.Length > 0
                ? SearchByVectorAsync(projectId, refinedQuery, topK, cancellationToken)
                : Task.FromResult<IReadOnlyList<RagChunkMatch>>([]);
            var keywordTask = keywords.Count > 0
                ? SearchByKeywordsAsync(projectId, keywords, topK, cancellationToken)
                : Task.FromResult<IReadOnlyList<RagChunkMatch>>([]);

            await Task.WhenAll(vectorTask, keywordTask).ConfigureAwait(false);

            var fused = ReciprocalRankFusion(vectorTask.Result, keywordTask.Result, rrfK, topK);

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Hybrid RAG project {ProjectId}: vector={VectorCount}, keyword={KeywordCount}, fused={FusedCount}.",
                    projectId,
                    vectorTask.Result.Count,
                    keywordTask.Result.Count,
                    fused.Count);
            }

            return fused;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Hybrid RAG retrieval failed for project {ProjectId}; continuing without document context.",
                projectId);
            return [];
        }
    }

    private async Task<IReadOnlyList<RagChunkMatch>> SearchByVectorAsync(
        Guid projectId,
        string refinedQueryEn,
        int topK,
        CancellationToken cancellationToken)
    {
        var settings = ragSettings.Value;
        var minSimilarity = Math.Clamp(settings.MinSimilarity, 0.0, 1.0);
        var maxDistance = Math.Clamp(1.0 - minSimilarity, 0.0, 1.0);

        var embedding = await embeddingService
            .EmbedAsync(refinedQueryEn, cancellationToken)
            .ConfigureAwait(false);
        if (embedding.Length == 0)
        {
            return [];
        }

        var queryVector = new Vector(embedding);
        return await dbContext.ProjectDocumentChunks
            .AsNoTracking()
            .Where(chunk =>
                chunk.ProjectId == projectId
                && chunk.Embedding.CosineDistance(queryVector) <= maxDistance)
            .OrderBy(chunk => chunk.Embedding.CosineDistance(queryVector))
            .Take(topK)
            .Select(chunk => new RagChunkMatch
            {
                Id = chunk.Id,
                // Critical: always return original Text for prompt citations.
                Text = chunk.Text,
                FileName = chunk.Document.FileName,
                Distance = chunk.Embedding.CosineDistance(queryVector),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<RagChunkMatch>> SearchByKeywordsAsync(
        Guid projectId,
        IReadOnlyList<string> keywords,
        int topK,
        CancellationToken cancellationToken)
    {
        // GIN-friendly overlap: any stored keyword equals any query keyword (keywords stored lowercased).
        var keywordArray = keywords.ToArray();

        var rows = await dbContext.ProjectDocumentChunks
            .AsNoTracking()
            .Where(chunk =>
                chunk.ProjectId == projectId
                && chunk.Keywords.Any(k => keywordArray.Contains(k)))
            .Select(chunk => new
            {
                chunk.Id,
                chunk.Text,
                FileName = chunk.Document.FileName,
                chunk.Keywords,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(row =>
            {
                var overlap = row.Keywords.Count(k =>
                    keywordArray.Contains(k, StringComparer.OrdinalIgnoreCase));
                return new
                {
                    Match = new RagChunkMatch
                    {
                        Id = row.Id,
                        Text = row.Text,
                        FileName = row.FileName,
                        // Placeholder distance; RRF ordering is authoritative for keyword hits.
                        Distance = overlap > 0 ? Math.Max(0.0, 1.0 - (overlap / (double)keywordArray.Length)) : 1.0,
                    },
                    Overlap = overlap,
                };
            })
            .OrderByDescending(row => row.Overlap)
            .ThenBy(row => row.Match.Distance)
            .Take(topK)
            .Select(row => row.Match)
            .ToList();
    }

    private static IReadOnlyList<RagChunkMatch> ReciprocalRankFusion(
        IReadOnlyList<RagChunkMatch> vectorResults,
        IReadOnlyList<RagChunkMatch> keywordResults,
        int rrfConstant,
        int topK)
    {
        var scores = new Dictionary<Guid, double>();
        var byId = new Dictionary<Guid, RagChunkMatch>();

        Accumulate(vectorResults);
        Accumulate(keywordResults);

        return scores
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => byId[pair.Key].Distance)
            .Take(topK)
            .Select(pair => byId[pair.Key])
            .ToList();

        void Accumulate(IReadOnlyList<RagChunkMatch> ranked)
        {
            for (var rank = 0; rank < ranked.Count; rank++)
            {
                var match = ranked[rank];
                if (!byId.TryGetValue(match.Id, out var existing)
                    || match.Distance < existing.Distance)
                {
                    byId[match.Id] = match;
                }

                scores[match.Id] = scores.GetValueOrDefault(match.Id)
                    + (1.0 / (rrfConstant + rank + 1));
            }
        }
    }

    private static IReadOnlyList<string> NormalizeKeywords(IReadOnlyList<string>? keywords)
    {
        if (keywords is null || keywords.Count == 0)
        {
            return [];
        }

        return keywords
            .Select(k => k?.Trim().ToLowerInvariant() ?? string.Empty)
            .Where(k => k.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string? FormatContext(IReadOnlyList<RagChunkMatch> matches, int maxChunkChars)
    {
        if (matches.Count == 0)
        {
            return null;
        }

        var sections = new List<string>
        {
            "[PROJECT_DOCUMENTATION]",
        };

        var index = 1;
        foreach (var match in matches)
        {
            var source = string.IsNullOrWhiteSpace(match.FileName) ? "Unknown document" : match.FileName.Trim();
            // Inject original Text only (never EnglishText).
            var text = Truncate(match.Text?.Trim() ?? string.Empty, maxChunkChars);
            if (text.Length == 0)
            {
                continue;
            }

            sections.Add(string.Empty);
            sections.Add($"[{index}] Source: {source}");
            sections.Add(text);
            index++;
        }

        return index == 1 ? null : string.Join(Environment.NewLine, sections);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength].TrimEnd() + "…";
}
