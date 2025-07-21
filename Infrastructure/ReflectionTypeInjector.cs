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
                    throw new InvalidOperationException($"Required service {property.PropertyType} is not registered.");
                }

                continue;
            }

            property.SetValue(instance, service);
        }
    }
}
