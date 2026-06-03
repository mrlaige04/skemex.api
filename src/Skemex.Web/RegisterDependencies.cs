using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.OpenApi;
using Skemex.Domain.Services;
using Skemex.Web.Infrastructure;
using Skemex.Web.OpenApi;
using Skemex.Web.Services;

namespace Skemex.Web;

public static class RegisterDependencies
{
    public static IServiceCollection AddWeb(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FormOptions>(f =>
        {
            f.MultipartBodyLengthLimit = 100000000 * 2;
        });

        services.AddEndpointsApiExplorer();
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<BearerSecuritySchemeDocumentTransformer>();
            options.AddOperationTransformer<AuthorizeOperationTransformer>();
        });
        
        services.AddExceptionHandler<SkemexExceptionHandler>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        services.AddControllers();

        var origins = ResolveAllowedOrigins(configuration);
        services.AddCors(cors =>
        {
            cors.AddDefaultPolicy(policy =>
            {
                policy
                    .WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        return services;
    }
    
    private static string[] ResolveAllowedOrigins(IConfiguration configuration)
    {
        var csv = configuration["CORS_ALLOWED_ORIGINS"];
        if (!string.IsNullOrWhiteSpace(csv))
        {
            return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        var fromConfig = configuration.GetSection("AllowedOrigins").Get<string[]>();
        if (fromConfig is { Length: > 0 })
        {
            return fromConfig;
        }

        return ["http://localhost:4200"];
    }
}