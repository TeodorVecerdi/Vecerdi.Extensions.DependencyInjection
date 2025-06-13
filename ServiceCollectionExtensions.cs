using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Vecerdi.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions {
    public static IServiceCollection ConfigureWithoutInterceptors<TOptions>(this IServiceCollection services, IConfiguration configuration) where TOptions : class {
        services.AddOptions();
        services.AddSingleton<IOptionsChangeTokenSource<TOptions>>(new ConfigurationChangeTokenSource<TOptions>("", configuration));
        services.AddSingleton<IConfigureOptions<TOptions>>(new NamedConfigureFromConfigurationOptions<TOptions>("", configuration, null));
        return services;
    }
}
