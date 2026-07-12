using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;
using Skemex.Application.SaModels.SaTenants;

namespace Skemex.Application.SaFeatures.Commands.SaTenants.UpdateSaTenant;

public sealed class UpdateSaTenantCommandHandler(
    ICurrentUser currentUser,
    IBaseRepository<Tenant> tenantRepository,
    IUrlService urlService,
    IOptions<SuperAdminOptions> superAdminOptions)
    : ICommandHandler<UpdateSaTenantCommand, SaTenantDto>
{
    public async Task<ErrorOr<SaTenantDto>> Handle(
        UpdateSaTenantCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsSuperAdmin())
        {
            return Error.Forbidden("SuperAdmin.Required", "Platform administrator access is required.");
        }

        var tenant = await tenantRepository.GetAsync(
            filter: t => t.Id == request.TenantId,
            include: q => q.Include(t => t.Users),
            cancellationToken: cancellationToken);

        if (tenant is null)
        {
            return Error.NotFound("Tenant.NotFound", "Workspace was not found.");
        }

        var changed = false;

        if (request.Name is not null)
        {
            var name = request.Name.Trim();
            if (name.Length > 0 && !string.Equals(name, tenant.Name, StringComparison.Ordinal))
            {
                if (await tenantRepository.ExistsAsync(
                        t => t.Name == name && t.Id != tenant.Id,
                        cancellationToken: cancellationToken))
                {
                    return Error.Conflict("Tenant.NameTaken", "A company with this name already exists.");
                }

                tenant.Name = name;
                changed = true;
            }
        }

        if (request.Email is not null)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            if (email.Length > 0 && !string.Equals(email, tenant.Email, StringComparison.OrdinalIgnoreCase))
            {
                if (superAdminOptions.Value.MatchesEmail(email))
                {
                    return Error.Validation(
                        "User.SuperAdminEmailReserved",
                        "This email address is reserved and cannot be used.");
                }

                if (await tenantRepository.ExistsAsync(
                        t => t.Email == email && t.Id != tenant.Id,
                        cancellationToken: cancellationToken))
                {
                    return Error.Conflict("Tenant.EmailTaken", "A company with this email already exists.");
                }

                tenant.Email = email;
                changed = true;
            }
        }

        if (changed)
        {
            await tenantRepository.UpdateAsync(tenant, cancellationToken);
        }

        return new SaTenantDto
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Email = tenant.Email,
            CreatedAt = tenant.CreatedAt,
            LogoUrl = urlService.GetTenantLogoUrl(tenant.LogoBlobId),
            MemberCount = tenant.Users.Count,
        };
    }
}
