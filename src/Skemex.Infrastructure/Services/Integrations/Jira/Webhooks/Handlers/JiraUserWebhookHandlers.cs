using System.Text.Json;
using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skemex.Application.Services.Integrations.Jira.Webhooks;
using Skemex.Domain.Consts;
using Skemex.Domain.Entities.Integrations;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Infrastructure.Services.Integrations.Jira.Webhooks.Handlers;

public sealed class JiraUserCreatedWebhookHandler(
    UserManager<User> userManager,
    ITenantRepository<TenantUser> tenantUserRepository,
    ITenantRepository<ProjectUser> projectUserRepository,
    ITenantRepository<IntegrationMapping> mappingRepository,
    IBaseRepository<Role> roleRepository,
    IBaseRepository<UserRole> userRoleRepository,
    ILogger<JiraUserCreatedWebhookHandler> logger) : IJiraWebhookEventHandler
{
    public string EventKey => JiraWebhookEventCatalog.UserCreated;

    public Task<ErrorOr<Success>> HandleAsync(
        JiraWebhookEventContext context,
        CancellationToken cancellationToken = default) =>
        JiraUserWebhookShared.UpsertAsync(
            context,
            createIfMissing: true,
            userManager,
            tenantUserRepository,
            projectUserRepository,
            mappingRepository,
            roleRepository,
            userRoleRepository,
            logger,
            cancellationToken);
}

public sealed class JiraUserUpdatedWebhookHandler(
    UserManager<User> userManager,
    ITenantRepository<TenantUser> tenantUserRepository,
    ITenantRepository<ProjectUser> projectUserRepository,
    ITenantRepository<IntegrationMapping> mappingRepository,
    IBaseRepository<Role> roleRepository,
    IBaseRepository<UserRole> userRoleRepository,
    ILogger<JiraUserUpdatedWebhookHandler> logger) : IJiraWebhookEventHandler
{
    public string EventKey => JiraWebhookEventCatalog.UserUpdated;

    public Task<ErrorOr<Success>> HandleAsync(
        JiraWebhookEventContext context,
        CancellationToken cancellationToken = default) =>
        JiraUserWebhookShared.UpsertAsync(
            context,
            createIfMissing: false,
            userManager,
            tenantUserRepository,
            projectUserRepository,
            mappingRepository,
            roleRepository,
            userRoleRepository,
            logger,
            cancellationToken);
}

public sealed class JiraUserDeletedWebhookHandler(
    ITenantRepository<ProjectUser> projectUserRepository,
    ITenantRepository<IntegrationMapping> mappingRepository,
    ILogger<JiraUserDeletedWebhookHandler> logger) : IJiraWebhookEventHandler
{
    public string EventKey => JiraWebhookEventCatalog.UserDeleted;

    public async Task<ErrorOr<Success>> HandleAsync(
        JiraWebhookEventContext context,
        CancellationToken cancellationToken = default)
    {
        var info = JiraUserWebhookShared.ReadUser(context.Payload);
        if (info is null)
        {
            return Result.Success;
        }

        var mapping = await JiraUserWebhookShared.FindProjectUserMappingAsync(
            mappingRepository,
            context.Connection.Id,
            info,
            cancellationToken);
        if (mapping is null)
        {
            return Result.Success;
        }

        var projectUser = await projectUserRepository.GetAsync(
            filter: p => p.Id == mapping.LocalEntityId && p.ProjectId == context.Connection.ProjectId,
            cancellationToken: cancellationToken);

        await mappingRepository.DeleteAsync(mapping, cancellationToken);
        if (projectUser is not null)
        {
            await projectUserRepository.DeleteAsync(projectUser, cancellationToken);
            logger.LogInformation(
                "Removed project membership {ProjectUserId} after Jira user {AccountId} deletion",
                projectUser.Id,
                info.AccountId);
        }

        return Result.Success;
    }
}

internal static class JiraUserWebhookShared
{
    public static async Task<ErrorOr<Success>> UpsertAsync(
        JiraWebhookEventContext context,
        bool createIfMissing,
        UserManager<User> userManager,
        ITenantRepository<TenantUser> tenantUserRepository,
        ITenantRepository<ProjectUser> projectUserRepository,
        ITenantRepository<IntegrationMapping> mappingRepository,
        IBaseRepository<Role> roleRepository,
        IBaseRepository<UserRole> userRoleRepository,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var info = ReadUser(context.Payload);
        if (info is null)
        {
            return Result.Success;
        }

        var mapping = await FindProjectUserMappingAsync(
            mappingRepository,
            context.Connection.Id,
            info,
            cancellationToken);

        ProjectUser? projectUser = null;
        if (mapping is not null)
        {
            projectUser = await projectUserRepository.GetAsync(
                filter: p => p.Id == mapping.LocalEntityId && p.ProjectId == context.Connection.ProjectId,
                include: q => q.Include(p => p.User),
                cancellationToken: cancellationToken);
        }

        // Update-only: skip when this project has no mapped membership.
        if (!createIfMissing && projectUser is null && mapping is null)
        {
            return Result.Success;
        }

        User? user = projectUser?.User;
        if (user is null && !string.IsNullOrWhiteSpace(info.Email))
        {
            user = await userManager.FindByEmailAsync(info.Email);
        }

        if (user is null)
        {
            if (!createIfMissing)
            {
                return Result.Success;
            }

            if (string.IsNullOrWhiteSpace(info.Email))
            {
                logger.LogWarning(
                    "Cannot create project user from Jira account {AccountId}: email missing",
                    info.AccountId);
                return Result.Success;
            }

            var (first, last) = SplitDisplayName(info.DisplayName, info.Email);
            user = new User
            {
                UserName = info.Email,
                Email = info.Email,
                FirstName = first,
                LastName = last,
                EmailConfirmed = true,
            };
            var create = await userManager.CreateAsync(user);
            if (!create.Succeeded)
            {
                logger.LogWarning(
                    "Failed creating user for Jira {AccountId}: {Errors}",
                    info.AccountId,
                    string.Join("; ", create.Errors.Select(e => e.Description)));
                return Result.Success;
            }
        }
        else if (!string.IsNullOrWhiteSpace(info.DisplayName) || !string.IsNullOrWhiteSpace(info.Email))
        {
            var (first, last) = SplitDisplayName(info.DisplayName, user.Email ?? info.Email ?? "user");
            var dirty = false;
            if (!string.IsNullOrWhiteSpace(first) && user.FirstName != first)
            {
                user.FirstName = first;
                dirty = true;
            }

            if (!string.IsNullOrWhiteSpace(last) && user.LastName != last)
            {
                user.LastName = last;
                dirty = true;
            }

            if (!string.IsNullOrWhiteSpace(info.Email)
                && !string.Equals(user.Email, info.Email, StringComparison.OrdinalIgnoreCase))
            {
                user.Email = info.Email;
                user.UserName = info.Email;
                dirty = true;
            }

            if (dirty)
            {
                await userManager.UpdateAsync(user);
            }
        }

        if (createIfMissing)
        {
            var tenantUser = await tenantUserRepository.GetAsync(
                filter: t => t.TenantId == context.Connection.TenantId && t.UserId == user.Id,
                cancellationToken: cancellationToken);
            if (tenantUser is null)
            {
                await tenantUserRepository.AddAsync(
                    new TenantUser
                    {
                        Id = Guid.NewGuid(),
                        TenantId = context.Connection.TenantId,
                        UserId = user.Id,
                        Status = TenantUserStatus.Active,
                    },
                    cancellationToken);

                var role = await roleRepository.GetAsync(
                        filter: r =>
                            r.TenantId == context.Connection.TenantId && r.Name == RoleNames.Developer,
                        cancellationToken: cancellationToken)
                    ?? await roleRepository.GetAsync(
                        filter: r => r.TenantId == context.Connection.TenantId,
                        cancellationToken: cancellationToken);
                if (role is not null)
                {
                    var existingRole = await userRoleRepository.GetAsync(
                        filter: ur =>
                            ur.UserId == user.Id
                            && ur.RoleId == role.Id
                            && ur.TenantId == context.Connection.TenantId,
                        cancellationToken: cancellationToken);
                    if (existingRole is null)
                    {
                        await userRoleRepository.AddAsync(
                            new UserRole
                            {
                                Id = Guid.NewGuid(),
                                UserId = user.Id,
                                RoleId = role.Id,
                                TenantId = context.Connection.TenantId,
                            },
                            cancellationToken);
                    }
                }
            }
        }

        projectUser ??= await projectUserRepository.GetAsync(
            filter: p => p.ProjectId == context.Connection.ProjectId && p.UserId == user.Id,
            cancellationToken: cancellationToken);

        if (projectUser is null)
        {
            if (!createIfMissing)
            {
                return Result.Success;
            }

            projectUser = new ProjectUser
            {
                Id = Guid.NewGuid(),
                TenantId = context.Connection.TenantId,
                ProjectId = context.Connection.ProjectId,
                UserId = user.Id,
            };
            await projectUserRepository.AddAsync(projectUser, cancellationToken);
        }

        if (mapping is null && createIfMissing)
        {
            await mappingRepository.AddAsync(
                IntegrationMappingFactory.ForProjectUser(
                    context.Connection.TenantId,
                    context.Connection.ProjectId,
                    context.Connection.Id,
                    projectUser.Id,
                    info.AccountId,
                    info.Email,
                    new { info.AccountId, info.Email, info.DisplayName, userId = user.Id }),
                cancellationToken);
        }
        else if (mapping is not null)
        {
            mapping.ExternalEntityId = info.AccountId;
            mapping.ExternalEntityKey = info.Email ?? mapping.ExternalEntityKey;
            mapping.ExternalDataJson = IntegrationMappingFactory.SerializeExternalData(
                new { info.AccountId, info.Email, info.DisplayName, userId = user.Id });
            mapping.ProjectUserId = projectUser.Id;
            mapping.LocalEntityId = projectUser.Id;
            mapping.LocalEntityType = IntegrationLocalEntityTypes.ProjectUser;
            JiraMappingTouch.MarkActive(mapping);
            await mappingRepository.UpdateAsync(mapping, cancellationToken);
        }

        logger.LogInformation(
            "Synced Jira user {AccountId} → project user {ProjectUserId} (user {UserId}, createIfMissing={Create})",
            info.AccountId,
            projectUser.Id,
            user.Id,
            createIfMissing);
        return Result.Success;
    }

    public static async Task<IntegrationMapping?> FindProjectUserMappingAsync(
        ITenantRepository<IntegrationMapping> mappingRepository,
        Guid connectionId,
        JiraUserInfo info,
        CancellationToken cancellationToken)
    {
        var byAccount = await mappingRepository.GetAsync(
            filter: m =>
                m.ProjectIntegrationId == connectionId
                && m.LocalEntityType == IntegrationLocalEntityTypes.ProjectUser
                && m.ExternalEntityId == info.AccountId,
            cancellationToken: cancellationToken);
        if (byAccount is not null)
        {
            return byAccount;
        }

        if (string.IsNullOrWhiteSpace(info.Email))
        {
            return null;
        }

        return await mappingRepository.GetAsync(
            filter: m =>
                m.ProjectIntegrationId == connectionId
                && m.LocalEntityType == IntegrationLocalEntityTypes.ProjectUser
                && m.ExternalEntityKey == info.Email,
            cancellationToken: cancellationToken);
    }

    public static JiraUserInfo? ReadUser(JsonElement payload)
    {
        if (!payload.TryGetProperty("user", out var userEl) || userEl.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var accountId = userEl.TryGetProperty("accountId", out var accountEl)
            ? accountEl.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return null;
        }

        var email = userEl.TryGetProperty("emailAddress", out var emailEl)
            ? emailEl.GetString()?.Trim().ToLowerInvariant()
            : null;
        var displayName = userEl.TryGetProperty("displayName", out var nameEl)
            ? nameEl.GetString()
            : null;
        return new JiraUserInfo(accountId, email, displayName);
    }

    private static (string First, string Last) SplitDisplayName(string? displayName, string fallbackEmail)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            var parts = displayName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                return (parts[0], parts[0]);
            }

            return (parts[0], parts[1]);
        }

        var local = fallbackEmail.Split('@')[0];
        return (local, local);
    }

    public sealed record JiraUserInfo(string AccountId, string? Email, string? DisplayName);
}
