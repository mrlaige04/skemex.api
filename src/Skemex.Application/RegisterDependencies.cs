using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Features.Behaviours;

namespace Skemex.Application;

public static class RegisterDependencies
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatorsFromAssemblies(AppDomain.CurrentDomain.GetAssemblies());
        
        services.Scan(scan => scan.FromAssemblyOf<IBaseCommand>()
            .AddClasses(classes => classes.AssignableTo(typeof(IQueryHandler<,>)), publicOnly: false)
                .AsImplementedInterfaces()
                .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(ICommandHandler<,>)), publicOnly: false)
                .AsImplementedInterfaces()
                .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(ICommandHandler<>)), publicOnly: false)
                .AsImplementedInterfaces()
                .WithScopedLifetime());

        services.AddScoped<ISender, Sender>();
        services.AddTransient(typeof(IBehaviour<,>), typeof(LoggingBehaviour<,>));
        services.AddTransient(typeof(IBehaviour<,>), typeof(ValidationBehaviour<,>));

        return services;
    }
}