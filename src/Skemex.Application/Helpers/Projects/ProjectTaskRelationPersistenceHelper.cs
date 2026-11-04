using Skemex.Domain.Enums;

namespace Skemex.Application.Helpers.Projects;

/// <summary>Validates AI-proposed task relations before persistence (no cycle detection).</summary>
public static class ProjectTaskRelationPersistenceHelper
{
    public static bool TryParseRelationType(string? value, out TaskRelationType relationType)
    {
        relationType = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Enum.TryParse(value.Trim(), ignoreCase: true, out relationType)
               && Enum.IsDefined(relationType);
    }

    public static bool TryParseTaskId(string? value, out Guid taskId)
    {
        taskId = Guid.Empty;
        return !string.IsNullOrWhiteSpace(value)
               && Guid.TryParse(value.Trim(), out taskId)
               && taskId != Guid.Empty;
    }

    /// <summary>
    /// Drops self-links, unknown ids, and duplicates (existing or within the batch).
    /// </summary>
    public static IReadOnlyList<(Guid SourceId, Guid TargetId, TaskRelationType Type)> FilterCandidates(
        IEnumerable<(Guid SourceId, Guid TargetId, TaskRelationType Type)> candidates,
        IReadOnlySet<Guid> knownTaskIds,
        IReadOnlySet<(Guid SourceId, Guid TargetId, TaskRelationType Type)> existingKeys)
    {
        var accepted = new List<(Guid SourceId, Guid TargetId, TaskRelationType Type)>();
        var batchKeys = new HashSet<(Guid, Guid, TaskRelationType)>();

        foreach (var candidate in candidates)
        {
            if (candidate.SourceId == Guid.Empty || candidate.TargetId == Guid.Empty)
            {
                continue;
            }

            if (candidate.SourceId == candidate.TargetId)
            {
                continue;
            }

            if (!knownTaskIds.Contains(candidate.SourceId)
                || !knownTaskIds.Contains(candidate.TargetId))
            {
                continue;
            }

            var key = (candidate.SourceId, candidate.TargetId, candidate.Type);
            if (existingKeys.Contains(key) || !batchKeys.Add(key))
            {
                continue;
            }

            accepted.Add(candidate);
        }

        return accepted;
    }
}
