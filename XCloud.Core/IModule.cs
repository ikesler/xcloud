using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace XCloud.Core;

public interface IModule
{
    void AddServices(IServiceCollection services, IConfiguration configuration);
}
