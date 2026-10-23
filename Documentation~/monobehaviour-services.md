---
title: MonoBehaviour services
description: Registering MonoBehaviours as services, MonoSingleton<T>, and how singleton components persist.
order: 3
tags: [registration, singletons]
sources:
  - Extensions/MonoBehaviourServiceCollectionExtensions.cs
  - Internal/BehaviourServices.cs
  - Singletons/
  - SourceGenerator~/src/Vecerdi.Extensions.DependencyInjection.SourceGenerator/MonoBehaviourSingletonFactory.cs
---

# MonoBehaviour services

Components can be services too: an audio director, a dialog host, anything other services need to reach. The
`AddMonoBehaviour*` extensions register a `MonoBehaviour` type, and the container finds or creates the component when
the service is resolved.

## Registering components

```csharp
services.AddMonoBehaviourSingleton<AudioDirector>();
services.AddMonoBehaviourSingleton<IDialogHost, DialogHost>();
services.AddKeyedMonoBehaviourSingleton<IAudioChannel, MusicChannel>("music");
services.AddMonoBehaviourTransient<FloatingText>();
```

| Lifetime | What a resolve does |
|---|---|
| Singleton | Returns the current instance in the loaded scenes, inactive ones included; if there is none, creates a `GameObject` with the component |
| Transient | Creates a new `GameObject` with the component every time |

Singletons register as `<T>` or `<TService, TSingleton>`, the latter also keyed, plus the selector forms in
[Exposing part of a singleton](#exposing-part-of-a-singleton). Transients register as `<T>` or
`<TInterface, TImplementation>`, both also keyed.

To expose one singleton under several services, register it once per service. Every registration resolves the same
instance:

```csharp
services.AddMonoBehaviourSingleton<AudioDirector>();
services.AddMonoBehaviourSingleton<IAudioDirector, AudioDirector>();
```

A resolved component is still a normal `MonoBehaviour`: it wakes up, and if it derives from `BaseMonoBehaviour` it
receives its own `[Inject]` properties.

## How singletons resolve

The container does not cache a singleton component. Every resolve asks `MonoSingleton<T>` for the current instance,
which keeps the reference itself and looks the component up again once Unity destroys it. After a scene load destroys
a scene-placed singleton, the next resolve returns the one the new scene brought.

- **A singleton the container creates** is made `DontDestroyOnLoad`: it is application-scoped by definition, and it
  may be created before the first scene even loads (by a hosted service, say).
- **A scene-placed singleton** keeps the persistence its author chose (see the settings below).
- **Services are injected once per instance**, from the container itself rather than from the scope that resolved
  it, so a component that outlives the scope never holds that scope's services. A replacement instance gets its own
  injection on its first resolve.

> [!WARNING]
> The container follows a replacement, but a consumer that stored the old reference does not: a singleton service
> that took the component in its constructor, a static field, an `[Inject]` property on an application-scoped
> component. Hand a `Scene`-persistence singleton only to consumers that are destroyed with the same scene, or make it
> `Application`-persistent.

A factory registration is the most common way to store the old reference by accident: the container caches whatever a
`Singleton` or `Scoped` factory returns. The bundled analyzer reports one that resolves a `MonoSingleton<T>` as
**VDI0008**, and its code fix rewrites the registration onto `AddMonoBehaviourSingleton`:

```csharp
// VDI0008
services.AddSingleton<IAudioDirector>(sp => sp.GetRequiredService<AudioDirector>());
services.AddSingleton<IMixer>(sp => sp.GetRequiredService<AudioDirector>().Mixer);

// Fixed
services.AddMonoBehaviourSingleton<IAudioDirector, AudioDirector>();
services.AddMonoBehaviourSingleton<IMixer, AudioDirector>(audioDirector => audioDirector.Mixer);
```

The analyzer recognises a singleton component by its `MonoSingleton<T>` base type, so it doesn't see a factory that
resolves a plain `MonoBehaviour` registered with `AddMonoBehaviourSingleton`, or one that resolves the component
through an interface. A factory that also resolves other services is reported without a fix.

## Exposing part of a singleton

When the service is something the component owns rather than the component itself, register a selector. It receives
the current instance on every resolve:

```csharp
services.AddMonoBehaviourSingleton((AudioDirector director) => director.Mixer);
services.AddKeyedMonoBehaviourSingleton<IAudioChannel, AudioDirector>(KeyedService.AnyKey,
    (director, key) => director.GetChannel((string)key!));
```

Because the selector runs on every resolve, return state the component owns; don't construct something new in it.
The keyed form also receives the key the consumer asked for, so one registration under `KeyedService.AnyKey` can
serve every key.

## Disposable components

A singleton component can't implement `IDisposable` or `IAsyncDisposable`, and neither can what a selector returns.
The container would track the object for disposal on every resolve and dispose it that many times when it shuts down.
Registering such a type throws `NotSupportedException`. For a selector whose declared service type isn't disposable,
the exception comes from the first resolve that returns a disposable object. Release resources in `OnDestroy`
instead.

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

A component registered as a singleton without deriving from `MonoSingleton<T>` resolves the same way, but it can't
change either setting, so it always uses the defaults, and nothing rejects a duplicate.

> [!WARNING]
> Hosted services and post-initialization actions run before the first scene loads. Resolving a scene-placed singleton
> (`Throw`) from there fails with an exception that says so; resolve it from scene code, or after
> `IHostApplicationLifetime.ApplicationStarted`.

## See also

- [Injection](injection.md): how the components receive their own services.
- [Lifecycle](lifecycle.md): when hosted services run relative to the first scene.
