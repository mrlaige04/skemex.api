namespace Skemex.Application.Helpers.Projects;

public static class ProjectTaskCodeHelper
{
    public static string Format(string projectCode, int number)
    {
        return $"{projectCode.Trim().ToUpperInvariant()}-{number}";
    }
}
