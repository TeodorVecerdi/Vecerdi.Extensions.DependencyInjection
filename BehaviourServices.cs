using System.Reflection;
using MediaBrowser.DependencyInjection.Infrastructure;
using MediaBrowser.DependencyInjection.Interfaces;
using MediaBrowser.DependencyInjection.Singletons;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace MediaBrowser.DependencyInjection;

internal static class BehaviourServices {
    private static readonly InjectedInstancesTracker s_InjectedInstances = new();

    public static void InjectIntoMonoBehaviourProperties(IServiceProvider serviceProvider, MonoBehaviour instance) {
        var allowsMultiple = instance is IAllowsReinitialization;
        if (!allowsMultiple && s_InjectedInstances.Contains(instance)) {
            return;
        }

        var injectableProperties = DependencyInjectionCache.GetInjectableProperties(instance.GetType());
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

        if (instance is IPostInitializationCallbacks callbacks) {
            callbacks.OnServicesInitialized();
        }

        if (!allowsMultiple) {
            s_InjectedInstances.Add(instance);
        }
    }

    public static T CreateMonoBehaviour<T>(ServiceLifetime lifetime) where T : MonoBehaviour {
        return lifetime switch {
            ServiceLifetime.Singleton => CreateSingleton(),
            ServiceLifetime.Scoped or ServiceLifetime.Transient => CreateTransient(),
            _ => throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null),
        };

        static T CreateSingleton() => MonoSingleton<T>.Instance;
        static T CreateTransient() => new GameObject(typeof(T).Name).AddComponent<T>();
    }

    public static MonoBehaviour CreateMonoBehaviour(Type type, ServiceLifetime lifetime) {
        return lifetime switch {
            ServiceLifetime.Singleton => CreateSingleton(),
            ServiceLifetime.Scoped or ServiceLifetime.Transient => CreateTransient(),
            _ => throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null),
        };

        MonoBehaviour CreateTransient() => (MonoBehaviour)new GameObject(type.Name).AddComponent(type) ?? throw new InvalidOperationException($"Failed to create {type.Name} instance.");

        MonoBehaviour CreateSingleton() {
            var singletonType = typeof(MonoSingleton<>).MakeGenericType(type);
            var instanceProperty = singletonType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                                ?? throw new InvalidOperationException($"Could not find Instance property on {singletonType.Name}");
            return (MonoBehaviour)instanceProperty.GetValue(null)
                ?? throw new InvalidOperationException($"Failed to get instance of {type.Name}");
        }
    }
}
