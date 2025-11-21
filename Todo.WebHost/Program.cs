using System.Globalization;
using FastEndpoints;
using Serilog;
using Todo.Tasks.Module;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateLogger();

builder.Host.UseSerilog();

try
{
    Log.Information("Starting Todo.WebHost");

    // Register modules
    TaskModuleRegistrar.ConfigureServices(builder.Services, builder.Configuration);

    // Configure health checks
    var healthChecksBuilder = builder.Services.AddHealthChecks();
    TaskModuleRegistrar.ConfigureHealthChecks(healthChecksBuilder, builder.Configuration);

    var app = builder.Build();

    // Configure the HTTP request pipeline
    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    
    // Map FastEndpoints
    app.MapFastEndpoints();
    
    // Map health check endpoints
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false // Just checks if app is running
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
