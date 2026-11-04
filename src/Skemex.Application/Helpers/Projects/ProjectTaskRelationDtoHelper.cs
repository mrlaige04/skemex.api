using Skemex.Application.Models.Projects;
using Skemex.Domain.Entities.Projects;

namespace Skemex.Application.Helpers.Projects;

public static class ProjectTaskRelationDtoHelper
{
    public static IReadOnlyList<ProjectTaskRelationDto> MapForTask(
        Guid taskId,
        IEnumerable<ProjectTaskRelation> relations)
    {
        return relations
            .OrderBy(relation => relation.RelationType)
            .ThenBy(relation => RelatedCode(taskId, relation), StringComparer.OrdinalIgnoreCase)
            .ThenBy(relation => relation.CreatedAt)
            .Select(relation => Map(taskId, relation))
            .ToList();
    }

    public static ProjectTaskRelationDto Map(Guid taskId, ProjectTaskRelation relation)
    {
        var isOutgoing = relation.SourceTaskId == taskId;
        var related = isOutgoing ? relation.TargetTask : relation.SourceTask;

        return new ProjectTaskRelationDto
        {
            Id = relation.Id,
            RelationType = relation.RelationType.ToString(),
            Direction = isOutgoing ? "Outgoing" : "Incoming",
            RelatedTaskId = related?.Id
                ?? (isOutgoing ? relation.TargetTaskId : relation.SourceTaskId),
            RelatedTaskCode = related?.Code ?? string.Empty,
            RelatedTaskTitle = related?.Title ?? string.Empty,
            RelatedTaskType = related?.Type.ToString() ?? ProjectTaskType.Task.ToString(),
        };
    }

    private static string RelatedCode(Guid taskId, ProjectTaskRelation relation)
    {
        var related = relation.SourceTaskId == taskId ? relation.TargetTask : relation.SourceTask;
        return related?.Code ?? string.Empty;
    }
}
