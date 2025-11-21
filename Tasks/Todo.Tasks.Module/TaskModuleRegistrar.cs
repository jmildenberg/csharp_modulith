using FastEndpoints;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Todo.Shared.Kernel.Modules;

namespace Todo.Tasks.Module;

public class TaskModuleRegistrar : IRegisterModule
{
    public static IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Check if module is enabled
        if (!configuration.GetValue<bool>("Modules:Tasks:Enabled", true))
        {
            return services;
        }

        // Register module services here
        // services.AddScoped<ITaskService, TaskService>();

        // Register FastEndpoints from this module's assembly
        services.AddFastEndpoints(o =>
        {
            o.Assemblies = new[] { typeof(TaskModuleRegistrar).Assembly };
        });

        return services;
    }

    public static IHealthChecksBuilder ConfigureHealthChecks(IHealthChecksBuilder healthCheckBuilder, IConfiguration configuration)
    {
        // Check if module is enabled
        if (!configuration.GetValue<bool>("Modules:Tasks:Enabled", true))
        {
            return healthCheckBuilder;
        }

        // Register health checks here
        // healthCheckBuilder.AddSqlServer(
        //     configuration.GetConnectionString("TasksDb"),
        //     name: "tasks-db",
        //     tags: new[] { "ready", "db" }
        // );

        return healthCheckBuilder;
    }
}
