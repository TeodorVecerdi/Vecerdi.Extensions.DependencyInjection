using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vecerdi.Extensions.DependencyInjection.Infrastructure;
using Object = UnityEngine.Object;

namespace Vecerdi.Extensions.DependencyInjection.Internal;

[AutoStaticsCleanup]
public static partial class BehaviourServices {
    private static readonly InjectedInstancesTracker s_InjectedInstances = new();

    // Injectors resolved so far, so a type pays the resolver's lookup (a generated context's name switch, a chain of
    // contexts, the reflection fallback's allocation) once rather than on every injection. One cache per resolver: a
    // thread passing a different resolver starts a fresh cache instead of clearing one another thread may be reading,
    // so a cache never mixes two resolvers' injectors.
    private static InjectorCache? s_InjectorCache;

    // The keys are the user's types: they must not keep that code loaded once it is unloading.
    [ClearCacheBetweenCodeLoads]
    private static void ClearInjectorCache() => Volatile.Write(ref s_InjectorCache, null);

    public static void InjectIntoMonoBehaviourProperties(IServiceProvider serviceProvider, ITypeInjectorResolver resolver, MonoBehaviour instance) {
        var allowsMultiple = instance is IAllowsReinitialization;
        if (!allowsMultiple && IsAlreadyInjected(instance)) {
            return;
        }

        var injector = GetTypeInjector(resolver, instance.GetType());
        if (injector == null) {
            // This shouldn't happen if Resolver includes a reflection fallback, but for safety.
            throw new InvalidOperationException($"No injector found for type {instance.GetType()}.");
        }

        try {
            injector.Inject(serviceProvider, instance);
        } catch (Exception exception) {
            // The inner exception says what is missing; this says who needed it. Unity prints the chain innermost first
            // and, for an exception escaping Awake, attaches the component (clicking the entry pings it) and disables it.
            throw new InvalidOperationException($"Could not inject services into {instance.GetType().Name}{DescribeLocation(instance)}.", exception);
        }

        if (instance is IPostInitializationCallbacks callbacks) {
            callbacks.OnServicesInitialized();
        }

        if (!allowsMultiple) {
            MarkInjected(instance);
        }
    }

    public static T CreateMonoBehaviour<T>(ServiceLifetime lifetime) where T : MonoBehaviour {
        return lifetime switch {
            ServiceLifetime.Singleton => CreateSingleton(),
            ServiceLifetime.Scoped or ServiceLifetime.Transient => CreateTransient(),
            _ => throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null),
        };

        static T CreateTransient() => new GameObject(typeof(T).Name).AddComponent<T>();

        static T CreateSingleton() {
            if (MonoSingleton<T>.InstanceExists) {
                return MonoSingleton<T>.Instance;
            }

            // A scene-placed instance keeps whatever persistence its author chose. One the container has to create
            // is application-scoped by definition - and it may be created before the first scene loads (hosted
            // services, post-initialization actions), which would destroy it along with everything else that is
            // not DontDestroyOnLoad while the container keeps handing out the dead reference.
            var existing = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
            T instance;
            try {
                instance = MonoSingleton<T>.Instance;
            } catch (InvalidOperationException exception) when (!SceneManager.GetActiveScene().isLoaded) {
                throw ResolvedBeforeFirstScene(typeof(T), exception);
            }

            if (existing == null) {
                Object.DontDestroyOnLoad(instance.gameObject);
            }

            return instance;
        }
    }

    private static ITypeInjector? GetTypeInjector(ITypeInjectorResolver resolver, Type type) {
        var cache = Volatile.Read(ref s_InjectorCache);
        if (cache is null || !ReferenceEquals(cache.Resolver, resolver)) {
            cache = new InjectorCache(resolver);
            Volatile.Write(ref s_InjectorCache, cache);
        }

        return cache.GetTypeInjector(type);
    }

    // The hierarchy can only be read on the main thread; injected from another one, the type name alone has to do.
    private static string DescribeLocation(MonoBehaviour instance) {
        try {
            return $" on '{HierarchyPath(instance.transform)}'";
        } catch (UnityException) {
            return "";
        }
    }

    private static string HierarchyPath(Transform transform) {
        var path = transform.name;
        for (var parent = transform.parent; parent != null; parent = parent.parent) {
            path = $"{parent.name}/{path}";
        }

        return path;
    }

    private static bool IsAlreadyInjected(MonoBehaviour instance) {
        // BaseMonoBehaviour carries its own flag, so the common case never touches the tracker. Any other MonoBehaviour
        // (InjectServices() is an extension on MonoBehaviour and works on foreign base classes) falls back to the
        // id-based tracker.
        return instance is BaseMonoBehaviour baseMonoBehaviour
            ? baseMonoBehaviour.ServicesInjected
            : s_InjectedInstances.Contains(instance);
    }

    private static void MarkInjected(MonoBehaviour instance) {
        if (instance is BaseMonoBehaviour baseMonoBehaviour) {
            baseMonoBehaviour.ServicesInjected = true;
        } else {
            s_InjectedInstances.Add(instance);
        }
    }

    private static InvalidOperationException ResolvedBeforeFirstScene(Type type, Exception inner) => new(
        $"{type.Name} is a scene-placed singleton (MissingSingletonInstanceBehavior.Throw) but was resolved before the first scene loaded. " +
        "Hosted services and post-initialization actions run before any scene exists; resolve scene-placed singletons from scene code or after IHostApplicationLifetime.ApplicationStarted.",
        inner);

    // Lookups never lock. Two threads missing the same type at once both resolve it, and whichever injector is stored
    // first is the one both get back; null results are not cached.
    private sealed class InjectorCache(ITypeInjectorResolver resolver) {
        private readonly ConcurrentDictionary<Type, ITypeInjector> m_Injectors = new();

        public ITypeInjectorResolver Resolver => resolver;

        public ITypeInjector? GetTypeInjector(Type type) {
            if (m_Injectors.TryGetValue(type, out var injector)) {
                return injector;
            }

            injector = resolver.GetTypeInjector(type);
            return injector is null ? null : m_Injectors.GetOrAdd(type, injector);
        }
    }
}
