using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Features.Commands.Users.DeleteUserProfileImage;
using Skemex.Application.Features.Commands.Users.UpdateUserProfile;
using Skemex.Application.Features.Queries.Users.GetCurrentUserProfile;
using Skemex.Application.Features.Queries.Users.GetProfileAvatarUrl;
using Skemex.Infrastructure.Authentication.ForgotPassword;
using Skemex.Infrastructure.Authentication.Invitations;
using Skemex.Infrastructure.Authentication.Login;
using Skemex.Infrastructure.Authentication.RefreshToken;
using Skemex.Infrastructure.Authentication.Register;
using Skemex.Infrastructure.Authentication.SelectTenant;
using Skemex.Infrastructure.Authentication.Session;
using Skemex.Web.Models;

namespace Skemex.Web.Controllers;

[Route("api/auth")]
public class AuthController(ISender sender) : BaseController
{
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCurrentUserProfileQuery(), cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpGet("session")]
    [Authorize]
    public async Task<IActionResult> GetSession(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCurrentUserSessionQuery(), cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpGet("profile/avatar-url")]
    [Authorize]
    public async Task<IActionResult> GetProfileAvatarUrl(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetProfileAvatarUrlQuery(), cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpGet("invitations/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetInvitation(string token, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTenantInvitationQuery { Token = token }, cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpPost("invitations/accept")]
    [AllowAnonymous]
    public async Task<IActionResult> AcceptInvitation(
        [FromBody] AcceptTenantInvitationCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpPost("password-reset/request")]
    [AllowAnonymous]
    public async Task<IActionResult> RequestPasswordReset(
        [FromBody] RequestPasswordResetCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpPost("password-reset/confirm")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmPasswordReset(
        [FromBody] ResetPasswordWithCodeCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpPost("tenant")]
    [Authorize]
    public async Task<IActionResult> SelectTenant([FromBody] SelectTenantCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpPatch("profile")]
    [Authorize]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdateProfile(
        [FromForm] UpdateUserProfileForm form,
        CancellationToken cancellationToken)
    {
        var command = new UpdateUserProfileCommand
        {
            FirstName = form.FirstName,
            LastName = form.LastName,
        };

        if (form.Image is { Length: > 0 })
        {
            var ms = new MemoryStream();
            await form.Image.CopyToAsync(ms, cancellationToken);
            ms.Position = 0;
            command.ProfileImage = ms;
            command.ProfileImageContentType = form.Image.ContentType;
            command.ProfileImageFileName = form.Image.FileName;
        }

        var result = await sender.Send(command, cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpDelete("profile-image")]
    [Authorize]
    public async Task<IActionResult> DeleteProfileImage(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteUserProfileImageCommand(), cancellationToken);
        return result.Match(Ok, Problem);
    }
}
