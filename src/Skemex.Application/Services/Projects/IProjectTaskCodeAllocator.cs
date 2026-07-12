namespace Skemex.Application.Services.Projects;

public interface IProjectTaskCodeAllocator
{
    Task<int> AllocateNextNumberAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken = default);
}

public static class ProjectTaskCodeFormatter
{
    public static string Format(string projectCode, int number)
    {
        return $"{projectCode.Trim().ToUpperInvariant()}-{number}";
    }
}
