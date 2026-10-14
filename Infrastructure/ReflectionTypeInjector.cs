using Microsoft.Extensions.DependencyInjection;

namespace Vecerdi.Extensions.DependencyInjection.Infrastructure;

internal sealed class ReflectionTypeInjector(Type type) : ITypeInjector {
    public void Inject(IServiceProvider serviceProvider, object instance) {
        var injectableProperties = DependencyInjectionCache.GetInjectableProperties(type);
        foreach (var (property, serviceKey, isRequired) in injectableProperties) {
            var service = serviceKey is not null
                ? (serviceProvider as IKeyedServiceProvider)?.GetKeyedService(property.PropertyType, serviceKey)
                : serviceProvider.GetService(property.PropertyType);
            if (service is null) {
                if (isRequired) {
                    var key = serviceKey is not null ? $" with key '{serviceKey}'" : "";
                    throw new InvalidOperationException($"No service for type '{property.PropertyType}'{key} has been registered (property {property.Name}).");
                }

                continue;
            }

            property.SetValue(instance, service);
        }
    }
}
