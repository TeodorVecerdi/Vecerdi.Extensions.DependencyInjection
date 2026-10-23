using Microsoft.Extensions.DependencyInjection;
using UnityEngine;
using Vecerdi.Extensions.DependencyInjection.Internal;

namespace Vecerdi.Extensions.DependencyInjection;

internal sealed class MonoBehaviourSingletonAccessor<T>(IServiceProvider root) : IMonoBehaviourSingletonRegistration where T : MonoBehaviour {
    public Type Type { get; } = typeof(T);
    public MissingSingletonInstanceBehavior MissingInstanceBehavior => MonoSingleton<T>.MissingInstanceBehavior;
    public SingletonPersistence Persistence => MonoSingleton<T>.Persistence;

    public T GetInstance() {
        var instance = BehaviourServices.CreateMonoBehaviour<T>(ServiceLifetime.Singleton);
        BehaviourServices.InjectIntoMonoBehaviourProperties(root, ServiceManager.Resolver, instance);
        return instance;
    }
}
