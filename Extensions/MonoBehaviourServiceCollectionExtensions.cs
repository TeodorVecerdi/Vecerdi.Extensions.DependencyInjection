using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UnityEngine;
using Vecerdi.Extensions.DependencyInjection.Internal;

namespace Vecerdi.Extensions.DependencyInjection;

public static class MonoBehaviourServiceCollectionExtensions {
    extension(IServiceCollection services) {
        private void AddMonoBehaviourSingletonAccessor<T>() where T : MonoBehaviour {
            if (typeof(IDisposable).IsAssignableFrom(typeof(T)) || typeof(IAsyncDisposable).IsAssignableFrom(typeof(T)))
                throw DisposableNotSupported(typeof(T).Name);
            services.TryAddSingleton<MonoBehaviourSingletonAccessor<T>>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IMonoBehaviourSingletonRegistration, MonoBehaviourSingletonAccessor<T>>(sp => sp.GetRequiredService<MonoBehaviourSingletonAccessor<T>>()));
        }

        public IServiceCollection AddMonoBehaviourSingleton<T>() where T : MonoBehaviour {
            services.AddMonoBehaviourSingletonAccessor<T>();
            services.Add(new ServiceDescriptor(typeof(T), sp => sp.GetRequiredService<MonoBehaviourSingletonAccessor<T>>().GetInstance(), ServiceLifetime.Transient));
            return services;
        }

        public IServiceCollection AddMonoBehaviourSingleton<TService, TSingleton>() where TSingleton : MonoBehaviour, TService {
            services.AddMonoBehaviourSingletonAccessor<TSingleton>();
            services.Add(new ServiceDescriptor(typeof(TService), sp => sp.GetRequiredService<MonoBehaviourSingletonAccessor<TSingleton>>().GetInstance(), ServiceLifetime.Transient));
            return services;
        }

        public IServiceCollection AddMonoBehaviourSingleton<TService, TSingleton>(Func<TSingleton, TService> selector) where TSingleton : MonoBehaviour where TService : notnull {
            if (typeof(IDisposable).IsAssignableFrom(typeof(TService)) || typeof(IAsyncDisposable).IsAssignableFrom(typeof(TService)))
                throw DisposableNotSupported($"{typeof(TService).Name} forwarded from {typeof(TSingleton).Name}");
            services.AddMonoBehaviourSingletonAccessor<TSingleton>();
            services.Add(new ServiceDescriptor(typeof(TService), sp => selector(sp.GetRequiredService<MonoBehaviourSingletonAccessor<TSingleton>>().GetInstance()) switch {
                IDisposable or IAsyncDisposable => throw DisposableNotSupported($"{typeof(TService).Name} forwarded from {typeof(TSingleton).Name}"),
                var service => service,
            }, ServiceLifetime.Transient));
            return services;
        }

        public IServiceCollection AddKeyedMonoBehaviourSingleton<TService, TSingleton>(object? serviceKey) where TSingleton : MonoBehaviour, TService {
            services.AddMonoBehaviourSingletonAccessor<TSingleton>();
            services.Add(new ServiceDescriptor(typeof(TService), serviceKey, (sp, _) => sp.GetRequiredService<MonoBehaviourSingletonAccessor<TSingleton>>().GetInstance(), ServiceLifetime.Transient));
            return services;
        }

        public IServiceCollection AddKeyedMonoBehaviourSingleton<TService, TSingleton>(object? serviceKey, Func<TSingleton, object?, TService> selector) where TSingleton : MonoBehaviour where TService : notnull {
            if (typeof(IDisposable).IsAssignableFrom(typeof(TService)) || typeof(IAsyncDisposable).IsAssignableFrom(typeof(TService)))
                throw DisposableNotSupported($"{typeof(TService).Name} forwarded from {typeof(TSingleton).Name}");
            services.AddMonoBehaviourSingletonAccessor<TSingleton>();
            services.Add(new ServiceDescriptor(typeof(TService), serviceKey, (sp, key) => selector(sp.GetRequiredService<MonoBehaviourSingletonAccessor<TSingleton>>().GetInstance(), key) switch {
                IDisposable or IAsyncDisposable => throw DisposableNotSupported($"{typeof(TService).Name} forwarded from {typeof(TSingleton).Name}"),
                var service => service,
            }, ServiceLifetime.Transient));
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

    private static NotSupportedException DisposableNotSupported(string subject) {
        return new NotSupportedException($"{subject} implements IDisposable or IAsyncDisposable and cannot be registered as a MonoBehaviour singleton: " +
                                         "the container would track it for disposal on every resolve and dispose it that many times at shutdown. Release resources in OnDestroy instead.");
    }
}
