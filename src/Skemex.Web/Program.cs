using Hangfire;
using Scalar.AspNetCore;
using Serilog;
using Skemex.Application;
using Skemex.Infrastructure;
using Skemex.Web;
using Skemex.Web.Extensions;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services
        .AddApplication(builder.Configuration)
        .AddInfrastructure(builder.Configuration)
        .AddWeb(builder.Configuration);

    var app = builder.Build();

    await app.MigrateDatabase();
    await app.EnsureSuperAdminCreated();

    app.UseSerilogRequestLogging();

    app.MapOpenApi();
    app.MapScalarApiReference();

    app.UseExceptionHandler(opt => { });

    app.UseRouting();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();

    if (app.Environment.IsDevelopment())
    {
        app.UseHangfireDashboard("/hangfire");
    }

    app.MapControllers();
    app.MapGet("ping", () => "Hello World!");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
