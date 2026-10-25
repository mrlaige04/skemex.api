using Microsoft.AspNetCore.Mvc;
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
using Skemex.Web.Models.Email;

namespace Skemex.Web.Controllers;

/// <summary>REMOVE: temporary SMTP smoke test.</summary>
[ApiController]
[Route("api/test")]
public sealed class TestEmailController(IEmailSender emailSender) : ControllerBase
{
    [HttpPost("email")]
    public async Task<IActionResult> SendTestEmail(
        [FromBody] SendTestEmailRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { error = "Email is required." });
        }

        await emailSender.SendEmailAsync(
            email,
            "Skemex test email",
            "<p>If you received this message, SMTP is configured correctly.</p>",
            isHtml: true,
            cancellationToken).ConfigureAwait(false);

        return Ok(new { sent = true, to = email });
    }
}
