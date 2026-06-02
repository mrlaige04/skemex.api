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

        var origins = configuration.GetSection("AllowedOrigins").Get<string[]>();
        services.AddCors(cors =>
        {
            cors.AddDefaultPolicy(policy =>
            {
                policy
                    .WithOrigins(origins ?? ["http://localhost:4200"])
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
        
        return services;
    }
}