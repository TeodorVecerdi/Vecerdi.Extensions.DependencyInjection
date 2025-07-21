using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;
using Vecerdi.Extensions.DependencyInjection.Infrastructure;

namespace Vecerdi.Extensions.DependencyInjection;

internal static class BehaviourServices {
    private static readonly InjectedInstancesTracker s_InjectedInstances = new();

    public static void InjectIntoMonoBehaviourProperties(IServiceProvider serviceProvider, ITypeInjectorResolver resolver, MonoBehaviour instance) {
        var allowsMultiple = instance is IAllowsReinitialization;
        if (!allowsMultiple && s_InjectedInstances.Contains(instance)) {
            return;
        }

        var injector = resolver.GetTypeInjector(instance.GetType());
        if (injector == null) {
            // This shouldn't happen if Resolver includes a reflection fallback, but for safety.
            throw new InvalidOperationException($"No injector found for type {instance.GetType()}.");
        }

        injector.Inject(serviceProvider, instance);

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
