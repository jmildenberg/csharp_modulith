using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Todo.Shared.Kernel.Modules;

public interface IRegisterModule
{
    static abstract IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration);
    static abstract IHealthChecksBuilder ConfigureHealthChecks(IHealthChecksBuilder healthCheckBuilder, IConfiguration configuration);
}
