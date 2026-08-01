using System.IdentityModel.Tokens.Jwt;
using Hangfire;
using Hangfire.PostgreSql;
using Minio;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services;
using Skemex.Application.Services.Ai;
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
using Skemex.Infrastructure.Services.Ai;
using Skemex.Infrastructure.Services.Ai.Providers;
using Skemex.Infrastructure.Storage;

namespace Skemex.Infrastructure;

public static class RegisterDependencies
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddDatabase(services, configuration);
        AddStorage(services, configuration);
        AddAi(services, configuration);
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

    private static void AddAi(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));
        services.PostConfigure<AiOptions>(options =>
        {
            EnsureDefaultGroqProvider(options);
            ApplyGroqApiKeyFromEnvironment(options);
        });
        services.AddScoped<IAiChatService, AiChatService>();
        services.AddScoped<IAiTaskDecompositionService, AiTaskDecompositionService>();
        services.AddScoped<IAiModelCatalogService, AiModelCatalogService>();
        services.AddSingleton<IAiProviderResolver, AiProviderResolver>();

        var aiOptions = configuration.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();
        EnsureDefaultGroqProvider(aiOptions);

        RegisterProviderIfEnabled(
            services,
            aiOptions,
            AiProviderNames.Groq,
            defaultBaseUrl: "https://api.groq.com/openai/v1",
            httpClientName: GroqAiProvider.HttpClientName,
            register: () => services.AddSingleton<IAiProvider, GroqAiProvider>());

        // Register additional IAiProvider implementations the same way (e.g. OpenRouterAiProvider).

        if (string.IsNullOrWhiteSpace(aiOptions.ActiveProvider))
        {
            throw new InvalidOperationException("Ai:ActiveProvider is required.");
        }
    }

    private static void RegisterProviderIfEnabled(
        IServiceCollection services,
        AiOptions aiOptions,
        string providerName,
        string defaultBaseUrl,
        string httpClientName,
        Action register)
    {
        if (!aiOptions.TryGetProvider(providerName, out var providerOptions)
            || providerOptions is null
            || !providerOptions.Enabled)
        {
            return;
        }

        var baseUrl = string.IsNullOrWhiteSpace(providerOptions.BaseUrl)
            ? defaultBaseUrl
            : providerOptions.BaseUrl.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(providerOptions.BaseUrl))
        {
            providerOptions.BaseUrl = defaultBaseUrl;
        }

        var timeoutSeconds = providerOptions.TimeoutSeconds > 0
            ? providerOptions.TimeoutSeconds
            : 120;

        services.AddHttpClient(httpClientName, client =>
        {
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        });

        register();
    }

    /// <summary>
    /// Seeds Groq defaults when <c>Ai:Providers</c> is empty so local/dev configs stay minimal.
    /// </summary>
    private static void EnsureDefaultGroqProvider(AiOptions options)
    {
        options.Providers ??= new Dictionary<string, AiProviderOptions>(StringComparer.OrdinalIgnoreCase);

        if (options.Providers.Count > 0)
        {
            return;
        }

        options.Providers[AiProviderNames.Groq] = new AiProviderOptions
        {
            ApiKey = string.Empty,
            BaseUrl = "https://api.groq.com/openai/v1",
            TimeoutSeconds = 120,
            Enabled = true,
        };
    }

    /// <summary>
    /// Allows <c>GROQ_API_KEY</c> env / .env to fill <c>Ai:Providers:Groq:ApiKey</c> when unset.
    /// </summary>
    private static void ApplyGroqApiKeyFromEnvironment(AiOptions options)
    {
        if (!options.TryGetProvider(AiProviderNames.Groq, out var groq) || groq is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(groq.ApiKey))
        {
            return;
        }

        var fromEnv = Environment.GetEnvironmentVariable("GROQ_API_KEY");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            groq.ApiKey = fromEnv.Trim();
        }
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
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Default is required to configure Hangfire.");
        }

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));

        services.AddHangfireServer();
    }

    private static void AddEmailing(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.AddScoped<IEmailSender, SmtpEmailSender>();
    }
}
