using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.Features.Commands.Users.UpdateUserProfile;

public sealed class UpdateUserProfileCommand : ICommand<UpdateUserProfileResponse>
{
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    /// <summary>When set, the handler reads, uploads, then disposes this stream.</summary>
    public Stream? ProfileImage { get; set; }

    public string? ProfileImageContentType { get; set; }

    public string? ProfileImageFileName { get; set; }
}
