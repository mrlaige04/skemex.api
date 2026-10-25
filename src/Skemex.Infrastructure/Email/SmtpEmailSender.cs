using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Skemex.Application.Configuration;
using Skemex.Application.Configuration.Ai;
using Skemex.Application.Configuration.App;
using Skemex.Application.Configuration.Documents;
using Skemex.Application.Configuration.Email;
using Skemex.Application.Configuration.Rag;
using Skemex.Application.Configuration.Storage;
using Skemex.Application.Services.Ai.Agents;
using Skemex.Application.Services.Ai.Chat;
using Skemex.Application.Services.Ai.Completions;
using Skemex.Application.Services.Ai.Embeddings;
using Skemex.Application.Services.Ai.Providers;
using Skemex.Application.Services.Ai.Rag;
using Skemex.Application.Services.Ai.Usage;
using Skemex.Application.Services.Common;
using Skemex.Application.Services.Documents;
using Skemex.Application.Services.Email;
using Skemex.Application.Services.Encryption;
using Skemex.Application.Services.Notifications;
using Skemex.Application.Services.Projects;
using Skemex.Application.Services.Storage;
using Skemex.Application.Services.Users;

namespace Skemex.Infrastructure.Email;

public sealed class SmtpEmailSender(
    IOptions<SmtpOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendEmailAsync(
        string email,
        string subject,
        string message,
        bool isHtml = false,
        CancellationToken cancellationToken = default)
    {
        var smtp = options.Value;

        if (string.IsNullOrWhiteSpace(smtp.Server))
        {
            throw new InvalidOperationException("Smtp:Server is not configured.");
        }

        if (string.IsNullOrWhiteSpace(smtp.Password))
        {
            logger.LogWarning(
                "SMTP password is not configured; email to {Recipient} with subject {Subject} was not sent.",
                email,
                subject);
            return;
        }

        // SMTP must not be tied to the HTTP request lifetime (proxies/clients can abort early).
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(smtp.TimeoutSeconds));

        var mail = BuildMessage(smtp, email, subject, message, isHtml);

        try
        {
            using var client = new SmtpClient
            {
                Timeout = smtp.TimeoutSeconds * 1000,
            };

            logger.LogDebug(
                "Connecting to SMTP {Server}:{Port} for {Recipient}",
                smtp.Server,
                smtp.Port,
                email);

            await client.ConnectAsync(
                    smtp.Server,
                    smtp.Port,
                    ResolveSecureSocketOptions(smtp),
                    timeoutCts.Token)
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(smtp.Username))
            {
                await client.AuthenticateAsync(smtp.Username, smtp.Password, timeoutCts.Token)
                    .ConfigureAwait(false);
            }

            await client.SendAsync(mail, timeoutCts.Token).ConfigureAwait(false);
            await client.DisconnectAsync(true, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException)
        {
            logger.LogError(
                ex,
                "SMTP send timed out or was canceled for {Recipient} via {Server}:{Port}.",
                email,
                smtp.Server,
                smtp.Port);
            throw new InvalidOperationException(
                $"Failed to send email: SMTP operation timed out after {smtp.TimeoutSeconds}s.",
                ex);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "SMTP send failed for {Recipient} via {Server}:{Port}.",
                email,
                smtp.Server,
                smtp.Port);
            throw new InvalidOperationException($"Failed to send email: {ex.Message}", ex);
        }
    }

    private static MimeMessage BuildMessage(
        SmtpOptions smtp,
        string email,
        string subject,
        string message,
        bool isHtml)
    {
        var mail = new MimeMessage();
        mail.From.Add(new MailboxAddress(smtp.SenderName, smtp.SenderEmail));
        mail.To.Add(MailboxAddress.Parse(email));
        mail.Subject = subject;
        mail.Body = new TextPart(isHtml ? "html" : "plain") { Text = message };
        return mail;
    }

    private static SecureSocketOptions ResolveSecureSocketOptions(SmtpOptions options) =>
        options.Port switch
        {
            465 => SecureSocketOptions.SslOnConnect,
            587 => SecureSocketOptions.StartTls,
            _ => options.EnableSsl ? SecureSocketOptions.Auto : SecureSocketOptions.None,
        };
}
