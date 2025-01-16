using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace XCloud.Core;

public static class DIExtensions
{
    public static void AddModules(this IServiceCollection services, IConfiguration configuration, params Type[] moduleTypes)
    {
        var modules = moduleTypes.Select(Activator.CreateInstance).Cast<IModule>();
        foreach (var module in modules)
        {
            services.AddSingleton(module);
            module.AddServices(services, configuration);
        }
    }
}
