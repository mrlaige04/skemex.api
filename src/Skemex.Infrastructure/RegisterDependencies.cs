using System.IdentityModel.Tokens.Jwt;
using Minio;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Infrastructure.Authentication;
using Skemex.Infrastructure.Authentication.Services;
using Skemex.Infrastructure.Authentication.Tokens;
using Skemex.Infrastructure.Data;
using Skemex.Infrastructure.Data.Interceptors;
using Skemex.Infrastructure.Email;
using Skemex.Infrastructure.Services;
using Skemex.Infrastructure.Storage;

namespace Skemex.Infrastructure;

public static class RegisterDependencies
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddDatabase(services, configuration);
        AddStorage(services, configuration);
        AddAppAuthentication(services, configuration);
        AddBackgroundJobs(services, configuration);
        AddEmailing(services, configuration);
        services.Configure<AppOptions>(configuration.GetSection(AppOptions.SectionName));
        services.Configure<SuperAdminOptions>(configuration.GetSection(SuperAdminOptions.SectionName));
        services.AddScoped<IAuthEmailService, AuthEmailService>();
        services.AddScoped<IEmailTemplateService, EmailTemplateService>();
        services.AddSingleton<EmailTemplateFileLoader>();
        services.AddScoped<EmailTemplateSeeder>();
        services.Scan(scan => scan.FromAssemblyOf<SkemexDbContext>()
            .AddClasses(classes => classes.AssignableTo(typeof(IQueryHandler<,>)), publicOnly: false)
                .AsImplementedInterfaces()
                .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(ICommandHandler<,>)), publicOnly: false)
                .AsImplementedInterfaces()
                .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(ICommandHandler<>)), publicOnly: false)
                .AsImplementedInterfaces()
                .WithScopedLifetime());

        return services;
    }

    private static void AddStorage(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.AddOptions<ProfileImageStorageOptions>()
            .Bind(configuration.GetSection(ProfileImageStorageOptions.SectionName))
            .PostConfigure<IOptions<StorageOptions>>((profile, storage) =>
            {
                if (!string.IsNullOrWhiteSpace(profile.Bucket))
                {
                    return;
                }

                var s = storage.Value;
                profile.Bucket = string.Equals(s.Provider, StorageProviderNames.Production,
                    StringComparison.OrdinalIgnoreCase)
                    ? s.Minio.BrandingBucket
                    : StorageBucketNames.Resolve(s, StorageBucketKind.Branding);
            });

        services.AddScoped<IUrlService, StorageUrlService>();
        services.AddScoped<IProfileImageService, ProfileImageService>();
        services.AddScoped<IProjectLogoService, ProjectLogoService>();
        services.AddScoped<IProjectDocumentStorageService, ProjectDocumentStorageService>();

        var provider = configuration.GetValue<string>($"{StorageOptions.SectionName}:Provider")
                       ?? StorageProviderNames.Local;

        if (string.Equals(provider, StorageProviderNames.Production, StringComparison.OrdinalIgnoreCase))
        {
            var publicEndpoint = configuration["Storage:Minio:PublicEndpoint"];
            if (string.IsNullOrWhiteSpace(publicEndpoint))
            {
                throw new InvalidOperationException(
                    "Set Storage:Minio:PublicEndpoint (GitHub secret MINIO_PUBLIC_ENDPOINT): browser-reachable MinIO URL used in presigned download links.");
            }

            services.AddSingleton<IMinioClient>(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<StorageOptions>>().Value.Minio;
                if (string.IsNullOrWhiteSpace(opts.Endpoint))
                {
                    throw new InvalidOperationException(
                        "Storage:Minio:Endpoint is required when Storage:Provider is Production.");
                }

                if (string.IsNullOrWhiteSpace(opts.BrandingBucket))
                {
                    throw new InvalidOperationException(
                        "Storage:Minio:BrandingBucket is required when Storage:Provider is Production.");
                }

                if (string.IsNullOrWhiteSpace(opts.FilesBucket))
                {
                    throw new InvalidOperationException(
                        "Storage:Minio:FilesBucket is required when Storage:Provider is Production.");
                }

                if (string.IsNullOrWhiteSpace(opts.ProjectDocumentsBucket))
                {
                    throw new InvalidOperationException(
                        "Storage:Minio:ProjectDocumentsBucket is required when Storage:Provider is Production.");
                }

                return SkemexMinioClientFactory.Create(
                    sp.GetRequiredService<IOptions<StorageOptions>>().Value,
                    configuration,
                    opts.Endpoint);
            });

            services.AddScoped<IBlobStorageService, MinioBlobStorageService>();
        }
        else if (string.Equals(provider, StorageProviderNames.Local, StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IBlobStorageService, LocalDiskBlobStorageService>();
        }
        else
        {
            throw new InvalidOperationException(
                $"Storage:Provider must be '{StorageProviderNames.Local}' or '{StorageProviderNames.Production}'.");
        }
    }

    private static void AddDatabase(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");

        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();

        services.AddDbContext<SkemexDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseNpgsql(connectionString, npgsqlOptions =>
                npgsqlOptions.MigrationsAssembly(typeof(SkemexDbContext).Assembly));
        });

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<SkemexDbContext>());

        services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));
        services.AddScoped(typeof(ITenantRepository<>), typeof(TenantBaseRepository<>));
        services.AddScoped<IProjectTaskCodeAllocator, ProjectTaskCodeAllocator>();
    }

    private static void AddAppAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                         ?? throw new InvalidOperationException("JWT configuration section 'Auth' is missing.");

        ArgumentNullException.ThrowIfNull(jwtOptions);

        services.AddAuthorization();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.RequireHttpsMetadata = false;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = jwtOptions.ToTokenValidationParameters();
            });

        services
            .AddIdentityCore<User>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Tokens.PasswordResetTokenProvider = NumericEmailTokenProvider<User>.ProviderName;
                options.ClaimsIdentity.EmailClaimType = JwtRegisteredClaimNames.Email;
                options.ClaimsIdentity.UserNameClaimType = JwtRegisteredClaimNames.Name;
                options.ClaimsIdentity.UserIdClaimType = JwtRegisteredClaimNames.Sub;
            })
            .AddRoles<Role>()
            .AddEntityFrameworkStores<SkemexDbContext>()
            .AddDefaultTokenProviders()
            .AddTokenProvider<NumericEmailTokenProvider<User>>(NumericEmailTokenProvider<User>.ProviderName);

        services.Configure<NumericEmailTokenProviderOptions>(options =>
        {
            options.TokenLifespan = TimeSpan.FromMinutes(15);
        });

        ReplaceGlobalRoleValidatorWithTenantScoped(services);

        services.AddScoped<TokenService>();
    }

    private static void ReplaceGlobalRoleValidatorWithTenantScoped(IServiceCollection services)
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            var d = services[i];
            if (d.ServiceType == typeof(IRoleValidator<Role>) && d.ImplementationType == typeof(RoleValidator<Role>))
            {
                services.RemoveAt(i);
            }
        }

        services.AddScoped<IRoleValidator<Role>, TenantAwareRoleValidator>();
    }

    private static void AddBackgroundJobs(IServiceCollection services, IConfiguration configuration)
    {
    }

    private static void AddEmailing(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.AddScoped<IEmailSender, SmtpEmailSender>();
    }
}
