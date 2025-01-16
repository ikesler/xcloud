using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XCloud.Clipper.Api;
using XCloud.Core;

namespace XCloud.Clipper;

public class Module: IModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<IClipper, Impl.Clipper>();
    }
}
