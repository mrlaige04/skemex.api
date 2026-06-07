namespace Skemex.Infrastructure.Email;

internal static class EmailTemplatePlaceholderRenderer
{
    public static string Render(string template, IReadOnlyDictionary<string, string> placeholders)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template;
        }

        var result = template;
        foreach (var (key, value) in placeholders)
        {
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            result = result.Replace("{{" + key + "}}", value ?? string.Empty, StringComparison.Ordinal);
        }

        return result;
    }
}
