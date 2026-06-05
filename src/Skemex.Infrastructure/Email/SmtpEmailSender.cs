using FluentEmail.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Services;

namespace Skemex.Infrastructure.Email;

public sealed class SmtpEmailSender(
    IFluentEmailFactory fluentEmailFactory,
    IOptions<SmtpOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendEmailAsync(
        string email,
        string subject,
        string message,
        bool isHtml = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Server))
        {
            throw new InvalidOperationException("Smtp:Server is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.Password))
        {
            logger.LogWarning(
                "SMTP password is not configured; email to {Recipient} with subject {Subject} was not sent.",
                email,
                subject);
            return;
        }

        var result = await fluentEmailFactory
            .Create()
            .To(email)
            .Subject(subject)
            .Body(message, isHtml)
            .SendAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!result.Successful)
        {
            throw new InvalidOperationException(
                $"Failed to send email: {string.Join("; ", result.ErrorMessages)}");
        }
    }
}
