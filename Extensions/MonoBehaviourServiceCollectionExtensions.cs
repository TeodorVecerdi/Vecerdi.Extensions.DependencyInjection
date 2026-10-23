using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UnityEngine;
using Vecerdi.Extensions.DependencyInjection.Internal;

namespace Vecerdi.Extensions.DependencyInjection;

public static class MonoBehaviourServiceCollectionExtensions {
    extension(IServiceCollection services) {
        private void AddMonoBehaviourSingletonAccessor<T>() where T : MonoBehaviour {
            if (typeof(IDisposable).IsAssignableFrom(typeof(T)) || typeof(IAsyncDisposable).IsAssignableFrom(typeof(T))) {
                throw new NotSupportedException(
                    $"{typeof(T).Name} implements IDisposable or IAsyncDisposable and cannot be registered as a MonoBehaviour singleton: " +
                    "the container would track it for disposal on every resolve and dispose it that many times at shutdown. Release resources in OnDestroy instead.");
            }

            services.TryAddSingleton<MonoBehaviourSingletonAccessor<T>>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IMonoBehaviourSingletonRegistration, MonoBehaviourSingletonAccessor<T>>(sp => sp.GetRequiredService<MonoBehaviourSingletonAccessor<T>>()));
        }

        public IServiceCollection AddMonoBehaviourSingleton<T>() where T : MonoBehaviour {
            services.AddMonoBehaviourSingletonAccessor<T>();
            services.Add(new ServiceDescriptor(typeof(T), sp => sp.GetRequiredService<MonoBehaviourSingletonAccessor<T>>().GetInstance(), ServiceLifetime.Transient));
            return services;
        }

        public IServiceCollection AddMonoBehaviourSingleton<TInterface, TImplementation>() where TImplementation : MonoBehaviour, TInterface {
            services.AddMonoBehaviourSingletonAccessor<TImplementation>();
            services.Add(new ServiceDescriptor(typeof(TInterface), sp => sp.GetRequiredService<MonoBehaviourSingletonAccessor<TImplementation>>().GetInstance(), ServiceLifetime.Transient));
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

        public IServiceCollection AddKeyedMonoBehaviourSingleton<TInterface, TImplementation>(object? serviceKey) where TImplementation : MonoBehaviour, TInterface {
            services.AddMonoBehaviourSingletonAccessor<TImplementation>();
            services.Add(new ServiceDescriptor(typeof(TInterface), serviceKey, (sp, _) => sp.GetRequiredService<MonoBehaviourSingletonAccessor<TImplementation>>().GetInstance(), ServiceLifetime.Transient));
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
