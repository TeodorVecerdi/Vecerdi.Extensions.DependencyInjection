using Microsoft.Extensions.DependencyInjection;
using UnityEngine;
using Vecerdi.Extensions.DependencyInjection.Internal;

namespace Vecerdi.Extensions.DependencyInjection;

internal sealed class MonoBehaviourSingletonAccessor<T>(IServiceProvider root) : IMonoBehaviourSingletonRegistration where T : MonoBehaviour {
    public Type Type { get; } = typeof(T);
    public MissingSingletonInstanceBehavior MissingInstanceBehavior => MonoSingleton<T>.MissingInstanceBehavior;
    public SingletonPersistence Persistence => MonoSingleton<T>.Persistence;

    private EntityId m_InjectedId;

    public T GetInstance() {
        var instance = BehaviourServices.CreateMonoBehaviour<T>(ServiceLifetime.Singleton);

        var id = instance.GetEntityId();
        if (id != m_InjectedId) {
            BehaviourServices.InjectIntoMonoBehaviourProperties(root, ServiceManager.Resolver, instance);
            m_InjectedId = id;
        }

        return instance;
    }
}
