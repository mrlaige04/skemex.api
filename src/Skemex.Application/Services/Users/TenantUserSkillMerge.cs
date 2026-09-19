using Skemex.Domain.Entities.Users;

namespace Skemex.Application.Services.Users;

public static class TenantUserSkillMerge
{
    public static List<string> NormalizeSkills(IEnumerable<string>? skills)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var skill in skills ?? [])
        {
            var trimmed = skill.Trim();
            if (trimmed.Length == 0 || !seen.Add(trimmed))
            {
                continue;
            }

            result.Add(trimmed);
        }

        return result;
    }

    /// <summary>
    /// Appends missing default skills onto the user skill list (case-insensitive union).
    /// Returns true when the user skill list changed.
    /// </summary>
    public static bool MergeDefaultSkills(TenantUser tenantUser, IEnumerable<string>? defaultSkills)
    {
        tenantUser.Skills ??= [];
        var existing = new HashSet<string>(
            tenantUser.Skills.Where(skill => !string.IsNullOrWhiteSpace(skill)).Select(skill => skill.Trim()),
            StringComparer.OrdinalIgnoreCase);

        var changed = false;
        foreach (var skill in NormalizeSkills(defaultSkills))
        {
            if (!existing.Add(skill))
            {
                continue;
            }

            tenantUser.Skills.Add(skill);
            changed = true;
        }

        return changed;
    }
}
