using Skemex.Domain.Enums;

namespace Skemex.Infrastructure.Email;

public sealed class EmailTemplateFileLoader
{
    private readonly string _templatesDirectory;

    public EmailTemplateFileLoader()
        : this(Path.Combine(AppContext.BaseDirectory, "Templates"))
    {
    }

    public EmailTemplateFileLoader(string templatesDirectory)
    {
        _templatesDirectory = templatesDirectory;
    }

    public async Task<string> LoadBodyAsync(EmailTemplateType type, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_templatesDirectory, $"{type}.html");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Email template file was not found: {path}", path);
        }

        return await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
    }
}
