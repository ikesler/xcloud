using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XCloud.Core;
using XCloud.Sharing.Api;
using XCloud.Sharing.Impl;
using XCloud.Sharing.Impl.Renderers;
using XCloud.Sharing.Settings;

namespace XCloud.Sharing;

public class Module: IModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<IShareService, ShareService>();
        services.AddTransient<Crypto>();
        services.AddTransient<MarkdownRenderer>();
        services.AddTransient<ExcalidrawRenderer>();
        services.AddOptions<ShareSettings>().Bind(configuration.GetSection(nameof(ShareSettings)));
    }
}
