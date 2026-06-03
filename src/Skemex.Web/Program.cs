using Scalar.AspNetCore;
using Skemex.Application;
using Skemex.Infrastructure;
using Skemex.Web;
using Skemex.Web.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication(builder.Configuration)
    .AddInfrastructure(builder.Configuration)
    .AddWeb(builder.Configuration);

var app = builder.Build();

await app.MigrateDatabase();
await app.EnsureSuperAdminCreated();

app.MapOpenApi();
app.MapScalarApiReference();

app.UseExceptionHandler(opt => {});

app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("ping", () => "Hello World!");

app.Run();
