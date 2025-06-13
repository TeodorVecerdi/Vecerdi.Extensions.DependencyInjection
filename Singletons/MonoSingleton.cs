using System.Runtime.CompilerServices;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Vecerdi.Extensions.DependencyInjection;

// ReSharper disable StaticMemberInGenericType
[HideMonoScript]
public abstract class MonoSingleton<T> : BaseMonoBehaviour where T : MonoBehaviour {
    private static T? s_Instance;

    public static T Instance {
        get {
            if (s_Instance != null) {
                return s_Instance;
            }

            s_Instance = FindAnyObjectByType<T>(FindObjectsInactive.Include);
            if (s_Instance == null) {
                s_Instance = MissingInstanceBehavior switch {
                    MissingSingletonInstanceBehavior.Create => new GameObject(typeof(T).Name).AddComponent<T>(),
                    MissingSingletonInstanceBehavior.Throw => throw new InvalidOperationException($"No instance of {typeof(T).Name} found in the scene."),
                    _ => throw new ArgumentOutOfRangeException(nameof(MissingInstanceBehavior), MissingInstanceBehavior, null),
                };
            }

            if (Persistence is SingletonPersistence.Application) {
                DontDestroyOnLoad(s_Instance);
            }

            return s_Instance;
        }
    }

    protected static MissingSingletonInstanceBehavior MissingInstanceBehavior { get; set; } = MissingSingletonInstanceBehavior.Create;
    protected static SingletonPersistence Persistence { get; set; } = SingletonPersistence.Scene;

    protected override void Awake() {
        if (s_Instance == null) {
            s_Instance = this as T;
            base.Awake();
            return;
        }

        if (s_Instance == this) return;

        Destroy(this);
        Destroy(gameObject);
        throw new InvalidOperationException($"Duplicate instance of {typeof(T).Name} found in the scene.");
    }

    protected virtual void OnDestroy() {
        if (s_Instance == this) {
            s_Instance = null;
        }
    }

    static MonoSingleton() {
        // Ensure that T's static constructor is called before the first instance is accessed, allowing T to set static properties if needed.
        RuntimeHelpers.RunClassConstructor(typeof(T).TypeHandle);
    }
}
