using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace Vecerdi.Extensions.DependencyInjection;

public static class MonoBehaviourServiceCollectionExtensions {
    public static IServiceCollection AddMonoBehaviourSingleton<T>(this IServiceCollection services) where T : MonoBehaviour {
        services.Add(new ServiceDescriptor(typeof(T), sp => {
            var instance = BehaviourServices.CreateMonoBehaviour<T>(ServiceLifetime.Singleton);
            BehaviourServices.InjectIntoMonoBehaviourProperties(sp, instance);
            return instance;
        }, ServiceLifetime.Singleton));

        return services;
    }

    public static IServiceCollection AddKeyedMonoBehaviourSingleton<T>(this IServiceCollection services, object? serviceKey) where T : MonoBehaviour {
        services.Add(new ServiceDescriptor(typeof(T), serviceKey, (sp, _) => {
            var instance = BehaviourServices.CreateMonoBehaviour<T>(ServiceLifetime.Singleton);
            BehaviourServices.InjectIntoMonoBehaviourProperties(sp, instance);
            return instance;
        }, ServiceLifetime.Singleton));

        return services;
    }

    public static IServiceCollection AddMonoBehaviourSingleton(this IServiceCollection services, Type type) {
        if (!typeof(MonoBehaviour).IsAssignableFrom(type)) {
            throw new ArgumentException("Type must be a MonoBehaviour", nameof(type));
        }

        services.Add(new ServiceDescriptor(type, sp => {
            var instance = BehaviourServices.CreateMonoBehaviour(type, ServiceLifetime.Singleton);
            BehaviourServices.InjectIntoMonoBehaviourProperties(sp, instance);
            return instance;
        }, ServiceLifetime.Singleton));

        return services;
    }

    public static IServiceCollection AddKeyedMonoBehaviourSingleton(this IServiceCollection services, Type type, object? serviceKey) {
        if (!typeof(MonoBehaviour).IsAssignableFrom(type)) {
            throw new ArgumentException("Type must be a MonoBehaviour", nameof(type));
        }

        services.Add(new ServiceDescriptor(type, serviceKey, (sp, _) => {
            var instance = BehaviourServices.CreateMonoBehaviour(type, ServiceLifetime.Singleton);
            BehaviourServices.InjectIntoMonoBehaviourProperties(sp, instance);
            return instance;
        }, ServiceLifetime.Singleton));

        return services;
    }

    public static IServiceCollection AddMonoBehaviourSingleton<TInterface, TImplementation>(this IServiceCollection services) where TImplementation : MonoBehaviour, TInterface {
        services.Add(new ServiceDescriptor(typeof(TInterface), sp => {
            var instance = BehaviourServices.CreateMonoBehaviour<TImplementation>(ServiceLifetime.Singleton);
            BehaviourServices.InjectIntoMonoBehaviourProperties(sp, instance);
            return instance;
        }, ServiceLifetime.Singleton));

        return services;
    }

    public static IServiceCollection AddKeyedMonoBehaviourSingleton<TInterface, TImplementation>(this IServiceCollection services, object? serviceKey) where TImplementation : MonoBehaviour, TInterface {
        services.Add(new ServiceDescriptor(typeof(TInterface), serviceKey, (sp, _) => {
            var instance = BehaviourServices.CreateMonoBehaviour<TImplementation>(ServiceLifetime.Singleton);
            BehaviourServices.InjectIntoMonoBehaviourProperties(sp, instance);
            return instance;
        }, ServiceLifetime.Singleton));

        return services;
    }

    public static IServiceCollection AddMonoBehaviourTransient<T>(this IServiceCollection services) where T : MonoBehaviour {
        services.Add(new ServiceDescriptor(typeof(T), sp => {
            var instance = BehaviourServices.CreateMonoBehaviour<T>(ServiceLifetime.Transient);
            BehaviourServices.InjectIntoMonoBehaviourProperties(sp, instance);
            return instance;
        }, ServiceLifetime.Transient));

        return services;
    }

    public static IServiceCollection AddKeyedMonoBehaviourTransient<T>(this IServiceCollection services, object? serviceKey) where T : MonoBehaviour {
        services.Add(new ServiceDescriptor(typeof(T), serviceKey, (sp, _) => {
            var instance = BehaviourServices.CreateMonoBehaviour<T>(ServiceLifetime.Transient);
            BehaviourServices.InjectIntoMonoBehaviourProperties(sp, instance);
            return instance;
        }, ServiceLifetime.Transient));

        return services;
    }

    public static IServiceCollection AddMonoBehaviourTransient(this IServiceCollection services, Type type) {
        if (!typeof(MonoBehaviour).IsAssignableFrom(type)) {
            throw new ArgumentException("Type must be a MonoBehaviour", nameof(type));
        }

        services.Add(new ServiceDescriptor(type, sp => {
            var instance = BehaviourServices.CreateMonoBehaviour(type, ServiceLifetime.Transient);
            BehaviourServices.InjectIntoMonoBehaviourProperties(sp, instance);
            return instance;
        }, ServiceLifetime.Transient));

        return services;
    }

    public static IServiceCollection AddKeyedMonoBehaviourTransient(this IServiceCollection services, Type type, object? serviceKey) {
        if (!typeof(MonoBehaviour).IsAssignableFrom(type)) {
            throw new ArgumentException("Type must be a MonoBehaviour", nameof(type));
        }

        services.Add(new ServiceDescriptor(type, serviceKey, (sp, _) => {
            var instance = BehaviourServices.CreateMonoBehaviour(type, ServiceLifetime.Transient);
            BehaviourServices.InjectIntoMonoBehaviourProperties(sp, instance);
            return instance;
        }, ServiceLifetime.Transient));

        return services;
    }

    public static IServiceCollection AddMonoBehaviourTransient<TInterface, TImplementation>(this IServiceCollection services) where TImplementation : MonoBehaviour, TInterface {
        services.Add(new ServiceDescriptor(typeof(TInterface), sp => {
            var instance = BehaviourServices.CreateMonoBehaviour<TImplementation>(ServiceLifetime.Transient);
            BehaviourServices.InjectIntoMonoBehaviourProperties(sp, instance);
            return instance;
        }, ServiceLifetime.Transient));

        return services;
    }

    public static IServiceCollection AddKeyedMonoBehaviourTransient<TInterface, TImplementation>(this IServiceCollection services, object? serviceKey) where TImplementation : MonoBehaviour, TInterface {
        services.Add(new ServiceDescriptor(typeof(TInterface), serviceKey, (sp, _) => {
            var instance = BehaviourServices.CreateMonoBehaviour<TImplementation>(ServiceLifetime.Transient);
            BehaviourServices.InjectIntoMonoBehaviourProperties(sp, instance);
            return instance;
        }, ServiceLifetime.Transient));

        return services;
    }
}
