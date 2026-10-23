using Microsoft.Extensions.DependencyInjection;
using UnityEngine;
using Vecerdi.Extensions.DependencyInjection.Internal;

namespace Vecerdi.Extensions.DependencyInjection;

public static class MonoBehaviourServiceCollectionExtensions {
    extension(IServiceCollection services) {
        public IServiceCollection AddMonoBehaviourSingleton<T>() where T : MonoBehaviour {
            services.Add(new ServiceDescriptor(typeof(T), sp => {
                var instance = BehaviourServices.CreateMonoBehaviour<T>(ServiceLifetime.Singleton);
                BehaviourServices.InjectIntoMonoBehaviourProperties(sp, ServiceManager.Resolver, instance);
                return instance;
            }, ServiceLifetime.Singleton));

            return services;
        }

        public IServiceCollection AddMonoBehaviourSingleton<TInterface, TImplementation>() where TImplementation : MonoBehaviour, TInterface {
            services.Add(new ServiceDescriptor(typeof(TInterface), sp => {
                var instance = BehaviourServices.CreateMonoBehaviour<TImplementation>(ServiceLifetime.Singleton);
                BehaviourServices.InjectIntoMonoBehaviourProperties(sp, ServiceManager.Resolver, instance);
                return instance;
            }, ServiceLifetime.Singleton));

            return services;
        }

        public IServiceCollection AddMonoBehaviourTransient<T>() where T : MonoBehaviour {
            services.Add(new ServiceDescriptor(typeof(T), sp => {
                var instance = BehaviourServices.CreateMonoBehaviour<T>(ServiceLifetime.Transient);
                BehaviourServices.InjectIntoMonoBehaviourProperties(sp, ServiceManager.Resolver, instance);
                return instance;
            }, ServiceLifetime.Transient));

            return services;
        }

        public IServiceCollection AddMonoBehaviourTransient<TInterface, TImplementation>() where TImplementation : MonoBehaviour, TInterface {
            services.Add(new ServiceDescriptor(typeof(TInterface), sp => {
                var instance = BehaviourServices.CreateMonoBehaviour<TImplementation>(ServiceLifetime.Transient);
                BehaviourServices.InjectIntoMonoBehaviourProperties(sp, ServiceManager.Resolver, instance);
                return instance;
            }, ServiceLifetime.Transient));

            return services;
        }

        public IServiceCollection AddKeyedMonoBehaviourSingleton<T>(object? serviceKey) where T : MonoBehaviour {
            services.Add(new ServiceDescriptor(typeof(T), serviceKey, (sp, _) => {
                var instance = BehaviourServices.CreateMonoBehaviour<T>(ServiceLifetime.Singleton);
                BehaviourServices.InjectIntoMonoBehaviourProperties(sp, ServiceManager.Resolver, instance);
                return instance;
            }, ServiceLifetime.Singleton));

            return services;
        }

        public IServiceCollection AddKeyedMonoBehaviourSingleton<TInterface, TImplementation>(object? serviceKey) where TImplementation : MonoBehaviour, TInterface {
            services.Add(new ServiceDescriptor(typeof(TInterface), serviceKey, (sp, _) => {
                var instance = BehaviourServices.CreateMonoBehaviour<TImplementation>(ServiceLifetime.Singleton);
                BehaviourServices.InjectIntoMonoBehaviourProperties(sp, ServiceManager.Resolver, instance);
                return instance;
            }, ServiceLifetime.Singleton));

            return services;
        }

        public IServiceCollection AddKeyedMonoBehaviourTransient<T>(object? serviceKey) where T : MonoBehaviour {
            services.Add(new ServiceDescriptor(typeof(T), serviceKey, (sp, _) => {
                var instance = BehaviourServices.CreateMonoBehaviour<T>(ServiceLifetime.Transient);
                BehaviourServices.InjectIntoMonoBehaviourProperties(sp, ServiceManager.Resolver, instance);
                return instance;
            }, ServiceLifetime.Transient));

            return services;
        }

        public IServiceCollection AddKeyedMonoBehaviourTransient<TInterface, TImplementation>(object? serviceKey) where TImplementation : MonoBehaviour, TInterface {
            services.Add(new ServiceDescriptor(typeof(TInterface), serviceKey, (sp, _) => {
                var instance = BehaviourServices.CreateMonoBehaviour<TImplementation>(ServiceLifetime.Transient);
                BehaviourServices.InjectIntoMonoBehaviourProperties(sp, ServiceManager.Resolver, instance);
                return instance;
            }, ServiceLifetime.Transient));

            return services;
        }
    }
}
