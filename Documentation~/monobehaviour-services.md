---
title: MonoBehaviour services
description: Registering MonoBehaviours as services, MonoSingleton<T>, and how singleton components persist.
order: 3
tags: [registration, singletons]
sources:
  - Extensions/MonoBehaviourServiceCollectionExtensions.cs
  - Internal/BehaviourServices.cs
  - Singletons/
---

# MonoBehaviour services

Components can be services too: an audio director, a dialog host, anything other services need to reach. The
`AddMonoBehaviour*` extensions register a `MonoBehaviour` type, and the container finds or creates the component when
the service is first resolved.

## Registering components

```csharp
services.AddMonoBehaviourSingleton<AudioDirector>();
services.AddMonoBehaviourSingleton<IDialogHost, DialogHost>();
services.AddKeyedMonoBehaviourSingleton<IAudioChannel, MusicChannel>("music");
services.AddMonoBehaviourTransient<FloatingText>();
```

| Lifetime | What a resolve does |
|---|---|
| Singleton | Uses the instance in the loaded scenes, inactive ones included; if there is none, creates a `GameObject` with the component |
| Transient | Creates a new `GameObject` with the component every time |

Every registration comes in the forms shown above (`<T>`, `<TInterface, TImplementation>`, keyed), plus overloads
taking a `Type` for registration by reflection.

A resolved component is still a normal `MonoBehaviour`: it wakes up, and if it derives from `BaseMonoBehaviour` it
receives its own `[Inject]` properties.

## Singleton lifetime

The container caches a singleton once and never re-creates it, so the component must outlive every scene load:

- **A singleton the container creates** is made `DontDestroyOnLoad`: it is application-scoped by definition, and it
  may be created before the first scene even loads (by a hosted service, say).
- **A scene-placed singleton** keeps the persistence its author chose. In a project that loads more than one scene,
  make it application-scoped (below); otherwise the next scene load destroys it and the container keeps handing out
  the dead reference.

## Static access with MonoSingleton

Derive from `MonoSingleton<T>` for a component that is also reachable statically, through `T.Instance`. It is a
`BaseMonoBehaviour`, so it receives its own services, and it rejects a second instance: the duplicate destroys itself
and throws.

```csharp
using Vecerdi.Extensions.DependencyInjection;

public sealed class DialogHost : MonoSingleton<DialogHost>, IDialogHost {
    // Must be placed in the scene; Instance throws instead of creating one.
    static DialogHost() => MissingInstanceBehavior = MissingSingletonInstanceBehavior.Throw;

    [Inject] internal IAudioDirector Audio { get; set; } = null!;
}
```

Two settings shape a singleton, both set from the type's static constructor (which `MonoSingleton<T>` runs before
the first lookup):

| Setting | Values | Default |
|---|---|---|
| `MissingInstanceBehavior` | `Create` a `GameObject` when none is found, or `Throw` | `Create` |
| `Persistence` | `Scene` (destroyed with its scene) or `Application` (`DontDestroyOnLoad`) | `Scene` |

`InstanceExists` and `InstanceOrNull` check for the instance without creating one.

> [!WARNING]
> Hosted services and post-initialization actions run before the first scene loads. Resolving a scene-placed singleton
> (`Throw`) from there fails with an exception that says so; resolve it from scene code, or after
> `IHostApplicationLifetime.ApplicationStarted`.

## See also

- [Injection](injection.md): how the components receive their own services.
- [Lifecycle](lifecycle.md): when hosted services run relative to the first scene.
