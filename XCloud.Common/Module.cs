using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XCloud.Common.Api;
using XCloud.Common.Impl;
using XCloud.Core;
using XCloud.Storage.Api;
using XCloud.Storage.Impl.LocalFS;
using XCloud.Storage.Settings;

namespace XCloud.Common;

public class Module: IModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<ITemplater, Templater>();
    }
}
