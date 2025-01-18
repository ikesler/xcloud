using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XCloud.Core;
using XCloud.Ext.Storage;
using XCloud.Storage.Api;
using XCloud.Storage.Impl;
using XCloud.Storage.Impl.LocalFS;
using XCloud.Storage.Settings;

namespace XCloud.Storage;

public class Module: IModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<IStorageProvider, LocalFsStorageProvider>();
        services.AddTransient<IStorage, StorageDecorator>();
        services.AddOptions<StorageSettings>().Bind(configuration.GetSection(nameof(StorageSettings)));
    }
}
