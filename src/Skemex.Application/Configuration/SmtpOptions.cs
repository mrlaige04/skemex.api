namespace Skemex.Application.Configuration;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Server { get; set; } = string.Empty;

    public int Port { get; set; } = 465;

    public string SenderName { get; set; } = string.Empty;

    public string SenderEmail { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public bool EnableSsl { get; set; } = true;

    public int TimeoutSeconds { get; set; } = 30;
}
