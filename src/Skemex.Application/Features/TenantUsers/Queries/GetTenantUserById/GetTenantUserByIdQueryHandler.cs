using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;
namespace Skemex.Application.Features.TenantUsers.Queries.GetTenantUserById;

public sealed class GetTenantUserByIdQueryHandler(
    ICurrentUser currentUser,
    ITenantRepository<TenantUser> tenantUserRepository,
    IBaseRepository<UserRole> userRoleRepository,
    IProfileImageService profileImages)
    : IQueryHandler<GetTenantUserByIdQuery, TenantUserDto>
{
    public async Task<ErrorOr<TenantUserDto>> Handle(
        GetTenantUserByIdQuery request,
        CancellationToken cancellationToken)
    {
        var tenantIdResult = TenantUserContext.RequireTenantId(currentUser);
        if (tenantIdResult.IsError)
        {
            return tenantIdResult.Errors;
        }

        var tenantId = tenantIdResult.Value;
        var tenantUser = await tenantUserRepository.GetAsync(
            filter: tu => tu.UserId == request.UserId,
            include: q => q.Include(tu => tu.User),
            cancellationToken: cancellationToken);

        if (tenantUser is null)
        {
            return Error.NotFound("User.NotFound", "User was not found.");
        }

        var userRoles = await userRoleRepository.GetAllAsync(
            filter: ur => ur.TenantId == tenantId && ur.UserId == request.UserId,
            include: q => q.Include(ur => ur.Role),
            cancellationToken: cancellationToken);

        var user = tenantUser.User;
        var avatarUrl = await profileImages.GetAvatarUrlAsync(user.PhotoBlobId, cancellationToken).ConfigureAwait(false);

        return new TenantUserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            CreatedAt = user.CreatedAt,
            Roles = userRoles.Select(ur => ur.Role.Name).Where(n => n is not null).Cast<string>().OrderBy(n => n).ToList(),
            Status = tenantUser.Status,
            AvatarUrl = avatarUrl,
        };
    }
}
