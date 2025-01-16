using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Retry;
using XCloud.Core;
using XCloud.ReadEra.Constants;
using XCloud.ReadEra.Impl;

namespace XCloud.ReadEra;

public class Module: IModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<EBookNotesImporter>();
        services.AddResiliencePipeline(PollyPipelines.ReadEraImportBookmarks, builder =>
        {
            builder
                .AddRetry(new RetryStrategyOptions
                {
                    Delay = TimeSpan.FromSeconds(5),
                    MaxRetryAttempts = 5,
                });
        });
    }
}
