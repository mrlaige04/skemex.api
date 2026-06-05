using Microsoft.AspNetCore.Mvc;
using Skemex.Application.Services;
using Skemex.Web.Models;

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
