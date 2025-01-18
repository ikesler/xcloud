using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XCloud.Core;
using XCloud.Storage.Api;
using XCloud.Storage.Impl.LocalFS;
using XCloud.Storage.Settings;

namespace XCloud.Storage;

public class Module: IModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<IStorage, LocalFsStorage>();
        services.AddOptions<StorageSettings>().Bind(configuration.GetSection(nameof(StorageSettings)));
    }
}
