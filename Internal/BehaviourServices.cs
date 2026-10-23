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

    public static void InjectIntoMonoBehaviourProperties(IServiceProvider serviceProvider, ITypeInjectorResolver resolver, MonoBehaviour instance) {
        var allowsMultiple = instance is IAllowsReinitialization;
        if (!allowsMultiple && IsAlreadyInjected(instance)) {
            return;
        }

        var injector = resolver.GetTypeInjector(instance.GetType());
        if (injector == null) {
            // This shouldn't happen if Resolver includes a reflection fallback, but for safety.
            throw new InvalidOperationException($"No injector found for type {instance.GetType()}.");
        }

        try {
            injector.Inject(serviceProvider, instance);
        } catch (Exception exception) {
            // The inner exception says what is missing; this says who needed it. Unity prints the chain innermost first
            // and, for an exception escaping Awake, attaches the component (clicking the entry pings it) and disables it.
            throw new InvalidOperationException($"Could not inject services into {instance.GetType().Name} on '{HierarchyPath(instance.transform)}'.", exception);
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
}
