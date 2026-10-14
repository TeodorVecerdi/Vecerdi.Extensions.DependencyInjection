---
title: Injection
description: How MonoBehaviours receive services - the inject attributes, BaseMonoBehaviour, callbacks, and resolvers.
order: 2
tags: [injection]
sources:
  - Attributes/
  - BaseMonoBehaviour.cs
  - Extensions/MonoBehaviourExtensions.cs
  - Interfaces/
  - Infrastructure/
  - Internal/BehaviourServices.cs
---

# Injection

A `MonoBehaviour` can't take constructor parameters, so services arrive through properties instead: mark a property
with `[Inject]`, and the package assigns it from the container when the component wakes up.

## Marking properties

```csharp
using Vecerdi.Extensions.DependencyInjection;

public sealed class ScoreBoard : BaseMonoBehaviour {
    [Inject] internal IScoreService Scores { get; set; } = null!;
    [Inject(isRequired: false)] internal IAchievements? Achievements { get; set; }
    [InjectFromKeyedServices("music")] internal IAudioChannel Music { get; set; } = null!;

    private void Start() => Scores.Changed += Refresh;
}
```

| Attribute | Resolves with |
|---|---|
| `[Inject]` | `GetRequiredService<T>()`: a missing service throws |
| `[Inject(isRequired: false)]` | `GetService<T>()`: a missing service leaves `null` |
| `[InjectFromKeyedServices(key)]` | `GetRequiredKeyedService<T>(key)`; pass `isRequired: false` for the optional form |

An `IServiceProvider` property receives the provider itself. An `IEnumerable<T>` property receives every registered
`T`; arrays and the list and collection interfaces do too, but only with
[generated injection](source-generation.md).

Keep setters `public` or `internal`. Reflection can assign a private setter, but generated injectors can't: they
report a warning and skip the property.

## Triggering injection

Derive from `BaseMonoBehaviour` and injection happens in its `Awake`, before your own code runs. Override `Awake` if
you need it, and call `base.Awake()` first:

```csharp
protected override void Awake() {
    base.Awake(); // services are assigned here
    m_Cache = Scores.CreateCache();
}
```

For a component that can't change its base class, call `InjectServices()` yourself, usually from `Awake`:

```csharp
public sealed class LegacyHud : ThirdPartyPanel {
    [Inject] internal IScoreService Scores { get; set; } = null!;

    private void Awake() => this.InjectServices();
}
```

Each instance is injected once; repeated calls are ignored. Implement `IAllowsReinitialization` to have every call
inject again.

> [!WARNING]
> A component instantiated under an inactive parent doesn't run `Awake`, so it isn't injected until it is activated.
> Call `InjectServices()` on it yourself if you need its services before then.

## When a service is missing

A required service that can't be resolved fails that one component. The exception names the component and its place
in the hierarchy, and its inner exception says what was missing:

```
InvalidOperationException: No service for type 'IScoreService' has been registered.
Rethrow as InvalidOperationException: Could not inject services into ScoreBoard on 'Canvas/HUD/ScoreBoard'.
```

Thrown from `Awake`, it's handled like any exception escaping `Awake`: Unity logs it with the component attached, so
clicking the entry selects it, and disables the component, so its `OnEnable`, `Start` and `Update` never run. Other
components inject as usual. Properties assigned before the failure keep their values, `OnServicesInitialized` isn't
called, and the component isn't marked as injected, so a later `InjectServices()` tries again. Called from anywhere
else, `InjectServices()` throws to its caller.

## After injection

Implement `IPostInitializationCallbacks` to run code once the properties are set, for example when injection is
triggered from somewhere other than your own `Awake`:

```csharp
public sealed class ScoreBoard : BaseMonoBehaviour, IPostInitializationCallbacks {
    [Inject] internal IScoreService Scores { get; set; } = null!;

    public void OnServicesInitialized() => Refresh();
}
```

## Resolvers

`ServiceManager.Resolver` decides how a type's properties are assigned. The default,
`ReflectionTypeInjectorResolver`, discovers the attributed properties with reflection (cached per type). The bundled
generator produces a faster, reflection-free resolver; combine the two so generated injectors handle the types they
cover and reflection handles the rest:

```csharp
ServiceManager.Resolver = new TypeInjectorResolverCombiner(
    new GameTypeInjectorResolverContext(), // generated
    new ReflectionTypeInjectorResolver());
```

`TypeInjectorResolverCombiner` asks each resolver in order and uses the first injector it gets. Set the resolver
before anything is injected (a `SubsystemRegistration` method). See
[Source-generated injection](source-generation.md) for the generated side.

## See also

- [MonoBehaviour services](monobehaviour-services.md): registering components themselves as services.
- [Lifecycle](lifecycle.md): when the container exists and when it goes away.
