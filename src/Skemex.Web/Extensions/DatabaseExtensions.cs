using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Skemex.Domain.Consts;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Infrastructure.Data;

namespace Skemex.Web.Extensions;

public static class DatabaseExtensions
{
    public static async Task MigrateDatabase(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SkemexDbContext>();

        if (await context.Database.CanConnectAsync())
        {
            await context.Database.MigrateAsync();
            await context.Database.EnsureCreatedAsync();
            await app.EnsureSuperAdminCreated();
        }
    }

    public static async Task EnsureSuperAdminCreated(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var roleRepository = scope.ServiceProvider.GetRequiredService<IBaseRepository<Role>>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IBaseRepository<User>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();

        var superAdminEmail = configuration["SuperAdmin:Email"];
        var superAdminPassword = configuration["SuperAdmin:Password"];

        ArgumentNullException.ThrowIfNull(superAdminEmail, "SuperAdmin email");
        ArgumentNullException.ThrowIfNull(superAdminPassword, "SuperAdmin password");

        var superUser = await userManager.FindByEmailAsync(superAdminEmail);
        var superAdmin = new User
        {
            Email = superAdminEmail,
            UserName = superAdminEmail
        };

        if (superUser == null)
        {
            var result = await userManager.CreateAsync(superAdmin, superAdminPassword);
            if (!result.Succeeded)
            {
                throw new Exception(string.Join(Environment.NewLine, result.Errors.Select(e => e.Description)));
            }

            superUser = superAdmin;
        }

        if (!superUser.EmailConfirmed)
        {
            superUser.EmailConfirmed = true;
            await userManager.UpdateAsync(superUser);
        }

        var existingSuperRole = await roleRepository.GetAsync(
            r => r.Name == RoleNames.SuperAdmin && r.TenantId == null);

        if (existingSuperRole == null)
        {
            var result = await roleManager.CreateAsync(new Role
            {
                Name = RoleNames.SuperAdmin,
                IsSystem = true,
                TenantId = null
            });

            if (!result.Succeeded)
            {
                throw new Exception(string.Join(Environment.NewLine, result.Errors.Select(e => e.Description)));
            }

            existingSuperRole = await roleRepository.GetAsync(
                r => r.Name == RoleNames.SuperAdmin && r.TenantId == null);
        }

        var userWithRoles = await userRepository.GetAsync(
            u => u.Id == superUser!.Id,
            q => q.Include(u => u.UserRoles).ThenInclude(ur => ur.Role));

        if (userWithRoles!.UserRoles.All(ur =>
                !(ur.Role.Name == RoleNames.SuperAdmin && ur.TenantId == null)))
        {
            userWithRoles.UserRoles.Add(new UserRole
            {
                UserId = userWithRoles.Id,
                RoleId = existingSuperRole!.Id,
                TenantId = null
            });

            await userRepository.UpdateAsync(userWithRoles);
        }
    }
}
