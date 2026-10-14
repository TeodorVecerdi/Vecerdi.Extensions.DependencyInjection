# Vecerdi.Extensions.DependencyInjection

A modern dependency injection library for Unity that brings Microsoft's dependency injection container to Unity projects with seamless integration and automatic service injection.

It ships with a [source generator](#source-generated-injection) that replaces reflection-based injection with generated code.

## Features

- 🎯 **Property-based injection** using attributes
- 🔑 **Keyed services** support for advanced scenarios
- 🏗️ **MonoBehaviour integration** with automatic service creation
- 🚀 **Zero scene setup** — the container is built before the splash screen, no bootstrap GameObject
- 🧵 **Hosted services** (`IHostedService`) started during the splash, stopped on quit
- ⚡ **Performance optimized** with caching and optional source generation
- 🎛️ **Configuration management** using Microsoft.Extensions.Configuration
- 🔄 **Service lifecycle management** (Singleton, Transient)
- 🧩 **Modern C# features** support (up to C# 13)

## Requirements

- One of:
    - **Unity 6.5 or later** with [UnityRoslynUpdater](https://github.com/DaZombieKiller/UnityRoslynUpdater) to
      enable modern C# features (C# 13+) on the Mono runtime
    - **Unity 7 or later**, which runs on CoreCLR and ships the latest C# features out of the box
- Teardown runs from Unity's built-in play-mode lifecycle attributes (`[OnExitingPlayMode]`, in the editor and in
  players), which are not present before 6.5
- The following NuGet packages:
    - PolySharp
    - Microsoft.Extensions.DependencyInjection
    - Microsoft.Extensions.Hosting

## Installation

This library is designed to be embedded directly in your project. Add it as a submodule or download the source code and add it to your Unity project.

## Quick Start

### 1. Service Registration

Create a static method with the `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` attribute to register your services before Unity's lifecycle begins:

```csharp
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;
using Vecerdi.Extensions.DependencyInjection;

public static class ServiceRegistration
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void RegisterServices()
    {
        ServiceManager.RegisterServices((services, config) =>
        {
            // Register regular services
            services.AddSingleton<IGameSettings, GameSettings>();
            services.AddTransient<ILogger, UnityLogger>();
            
            // Register MonoBehaviour services
            services.AddMonoBehaviourSingleton<PlayerController>();
            services.AddMonoBehaviourSingleton<IUIManager, UIManager>();
            
            // Register keyed services
            services.AddKeyedMonoBehaviourSingleton<IWeaponController, SwordController>("sword");
            services.AddKeyedMonoBehaviourSingleton<IWeaponController, BowController>("bow");
        });
        
        // Optional: Register configuration
        ServiceManager.RegisterConfiguration(config =>
        {
            config.AddJsonFile("appsettings.json", optional: true);
        });
    }
}
```

### 2. Service Injection

#### Using BaseMonoBehaviour (Recommended)

Inherit from `BaseMonoBehaviour` for automatic service injection:

```csharp
using Vecerdi.Extensions.DependencyInjection;

public class PlayerController : BaseMonoBehaviour
{
    [Inject] public IGameSettings GameSettings { get; set; }
    [Inject] public ILogger Logger { get; set; }
    [Inject(isRequired: false)] public IOptionalService? OptionalService { get; set; }
    
    // Keyed service injection
    [InjectFromKeyedServices("sword")] public IWeaponController SwordController { get; set; }
    
    private void Start()
    {
        // Services are automatically injected in Awake()
        Logger.Log($"Player speed: {GameSettings.PlayerSpeed}");
    }
}
```

#### Manual Injection

For existing MonoBehaviour classes, call `InjectServices()` manually:

```csharp
public class ExistingController : MonoBehaviour
{
    [Inject] public IGameSettings GameSettings { get; set; }
    
    private void Awake()
    {
        this.InjectServices();
    }
}
```

### 3. Nothing to place

There is no `ServiceManager` component. The container builds itself from a
`[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]` hook, before any scene loads, so
every `Awake()` in your first scene already sees a ready provider.

## Lifecycle

```
SubsystemRegistration   your RegisterServices / RegisterConfiguration calls (they only queue callbacks)
BeforeSplashScreen      ServiceManager builds the provider, runs post-initialization actions,
                        then starts every IHostedService on the thread pool
   (splash screen)      hosted services run while Unity paints the splash
                        ApplicationStarted once every hosted service has started
Awake                   scene MonoBehaviours inject
quit / play-mode exit   ApplicationStopping (Application.quitting, everything still alive)
                        every OnDisable/OnDestroy runs, services still usable
[OnExitingPlayMode]     hosted services stop (reverse order), ApplicationStopped,
                        the provider and the configuration are disposed
```

- **`ServiceManager.Instance`** is the built provider. It throws before `BeforeSplashScreen`, so do not resolve
  services from `SubsystemRegistration` or `AfterAssembliesLoaded` hooks — queue a post-initialization action
  instead.
- **`ServiceManager.RegisterPostInitializationAction(Action<IServiceProvider>)`** runs synchronously on the main
  thread right after the provider is built and before hosted services start. Use it for one-off wiring that needs
  resolved services but no scene.
- **Hosted services start on the thread pool.** Their constructors and the synchronous head of `StartAsync` must
  not touch MonoBehaviour singletons or main-thread-only Unity APIs (`Application.persistentDataPath`, `Screen`,
  `GameObject` creation, …). Hop to the main thread explicitly where one is needed. Services start concurrently,
  so a service that depends on another should model that dependency rather than rely on registration order.
- **MonoBehaviour singletons live as long as the container.** The container caches a singleton once and never
  re-creates it, so a registered MonoBehaviour that Unity destroys on a scene load is handed out dead from then on.
  A singleton the container creates is therefore made `DontDestroyOnLoad`, overriding a `Persistence.Scene`
  setting; a scene-placed instance keeps its author's persistence, so give it `Persistence.Application` in any
  project that loads more than one scene. Scene-placed singletons cannot be resolved before the first scene loads
  (a hosted service or post-initialization action that tries gets an exception saying so).
- **`IHostApplicationLifetime`** is registered for the session (also `ServiceManager.Instance.Lifetime`):
  - `ApplicationStarted`: every hosted service has finished starting; startup failures are logged per service and
    do not hold it back. Skipped if the session is already stopping, so a waiter should also watch `ApplicationStopping`
    (or its own token). Callbacks run on the thread pool.
  - `ApplicationStopping`: the session is ending and every object is still alive. Fired from `Application.quitting`
    (a player quit, after `wantsToQuit` approved it; leaving play mode); closing the editor while playing skips
    `Application.quitting`, so the [code lifetime](#code-lifetime-vecerdiextensionslifetime) fires it there. Callbacks
    run on the main thread. It is also the token every `StartAsync` receives.
  - `ApplicationStopped`: hosted services have stopped (or timed out); the provider is about to be disposed. Main
    thread, during teardown: keep callbacks quick.
  - `StopApplication()` asks Unity to end the session: `Application.Quit()` in a player (vetoable through
    `wantsToQuit`), leaving play mode in the editor.
- **Teardown** runs from `[OnExitingPlayMode]`, which comes after every `OnDisable`/`OnDestroy` on each path that ends
  a session: leaving play mode, a player quitting, closing the editor while playing, and recompiling during play
  (objects only get `OnDisable` there, and nothing re-creates the container afterwards). Every script can therefore
  use its injected services from `OnDisable` and `OnDestroy`. The order of `OnDestroy` calls on quit is arbitrary,
  whatever the script execution order, so no object can stand in for this. Stopping the hosted services (reverse
  registration order, with a token that cancels at the deadline) and disposing the provider each get a bounded wait
  (5 s) so a stuck service cannot hang exit. The main thread is blocked during that wait: a `StopAsync` that needs
  the main thread to complete cannot, and the rest may run on the thread pool. Leaving play mode runs the same path,
  so re-entering play mode without a domain reload works.
- **Shutdown work that must run in a defined order** belongs in `IHostedService.StopAsync`.

## Code lifetime (`Vecerdi.Extensions.Lifetime`)

`Lifetime/` holds a second, independent assembly with no references: `CodeLifetime`, the lifetime of the currently
loaded code. It ends when the editor reloads code or quits, and when a player quits. The DI container covers one
play session; with domain reload off, many play sessions run inside one code lifetime. Anything that owns a thread,
socket, file watcher, timer or long-running loop outside a play session ties its shutdown to it, so nothing outlives
the code that started it. On CoreCLR that is what lets an unloaded code generation be collected.

```csharp
using Vecerdi.Extensions.Lifetime;

// Synchronous, on the main thread, with the reason; reverse registration order.
var registration = CodeLifetime.OnStopping("MyServer", ending => {
    if (ending == CodeLifetimeEnd.Reload) Suspend(); else Shutdown();
});

// Long-running loops observe the token.
while (!CodeLifetime.Stopping.IsCancellationRequested) { ... }

// A stop waits (2 s in total) for tracked work and threads, and reports whatever is still running.
CodeLifetime.Track("MyServer", backgroundWrite);
CodeLifetime.Track("MyServer", workerThread);
```

- **When it stops:** editor quit (`EditorApplication.quitting`, reason `Quit`), code reload (`[OnCodeDeinitializing]`,
  reason `Reload`), player quit (`Application.quitting`, after `wantsToQuit` approved it). Unity objects are still
  alive in all three. It stops once; registering afterwards runs the callback immediately.
- **The order of a stop:** `Stopping` is cancelled, the callbacks run (each isolated; one slower than 100 ms is
  reported), then tracked work and threads get what is left of the budget. The wait blocks the main thread, so
  tracked work must not need the main thread to finish.
- **Stragglers** are reported by owner name: in the editor at `[OnCodeUnloading]`, in a player right after the stop.
- **Asset import workers** run the same hooks. The lifetime is harmless there, but anything that starts a server
  should keep its own `AssetDatabase.IsAssetImportWorkerProcess()` check.
- Reference the `Vecerdi.Extensions.Lifetime` asmdef; it does not pull in the DI assembly or the
  Microsoft.Extensions packages.

## Advanced Usage

### Post-Initialization Callbacks

Implement `IPostInitializationCallbacks` to receive a callback after services are injected:

```csharp
public class MyController : BaseMonoBehaviour, IPostInitializationCallbacks
{
    [Inject] public IGameSettings GameSettings { get; set; }
    
    public void OnServicesInitialized()
    {
        // Called after all services are injected
        Debug.Log("Services ready!");
    }
}
```

### Allowing Re-injection

Implement `IAllowsReinitialization` to allow services to be re-injected:

```csharp
public class ReusableController : BaseMonoBehaviour, IAllowsReinitialization
{
    [Inject] public IGameSettings GameSettings { get; set; }
    
    // This MonoBehaviour can be re-injected multiple times
}
```

### Source-generated injection

The package ships a source generator (`SourceGenerator/`, built from `SourceGenerator~/`) that Unity applies to every
assembly referencing this one. In an assembly that declares a `partial` class deriving from
`TypeInjectorResolverContext`, it generates that class's `GetTypeInjector`: one injector per eligible `MonoBehaviour`
that assigns its `[Inject]` properties directly, with no reflection.

```csharp
using Vecerdi.Extensions.DependencyInjection.Infrastructure;

public partial class GameTypeInjectorResolverContext : TypeInjectorResolverContext;

// Generated injectors first; reflection for anything the context does not cover (other assemblies, excluded types).
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void UseGeneratedInjectors() {
    ServiceManager.Resolver = new TypeInjectorResolverCombiner(
        new GameTypeInjectorResolverContext(),
        new ReflectionTypeInjectorResolver());
}
```

- **Eligible types:** non-abstract classes in the same assembly deriving from `MonoBehaviour` that have at least one
  injectable property, plus every `BaseMonoBehaviour` (one without properties gets a shared no-op injector).
  `[ExcludeFromInjectionGeneration]` opts a type out, leaving it to the next resolver.
- **Injectable properties:** exactly one `[Inject]` or `[InjectFromKeyedServices]`, a public, internal or protected
  internal setter, not `init`-only. Inherited properties are included. `IServiceProvider` properties receive the
  provider itself; `IEnumerable<T>`, `T[]`, `IReadOnlyList<T>`/`IReadOnlyCollection<T>` and
  `List<T>`/`IList<T>`/`ICollection<T>` receive every registered `T`.
- **Service keys:** `string`, `bool`, `char`, the numeric types, or `null`.
- **One context per assembly.** A generic context is not supported.

| Code | Severity | Meaning |
|---|---|---|
| VDI0001 | Warning | Property is `init`-only and cannot be injected |
| VDI0002 | Warning | Generic context classes are not supported |
| VDI0003 | Info | More than one context in the assembly |
| VDI0004 | Info | No eligible types for the context |
| VDI0005 | Warning | Property has an inaccessible setter |
| VDI0006 | Error | Property has more than one inject attribute |
| VDI0007 | Warning | Service key ignored for an `IServiceProvider` property |

### Singleton MonoBehaviours

Create singleton MonoBehaviours with automatic service injection:

```csharp
public class GameManager : MonoSingleton<GameManager>
{
    [Inject] public IGameSettings GameSettings { get; set; }
    
    // Automatically becomes a singleton with service injection.
    // It can be accessed using `GameManager.Instance`.
}
```

## API Reference

### Attributes

- `[Inject(bool isRequired = true)]` - Marks a property for service injection
- `[InjectFromKeyedServices(object? serviceKey, bool isRequired = true)]` - Injects a keyed service

### ServiceManager

- `RegisterServices(Action<IServiceCollection, IConfigurationManager>)` - Queue service registrations (call from `SubsystemRegistration`)
- `RegisterConfiguration(Action<IConfigurationManager>)` - Queue configuration sources
- `RegisterPostInitializationAction(Action<IServiceProvider>)` - Run once the provider is built, before hosted services start
- `Instance` - The built provider (`IKeyedServiceProvider`); throws before initialization
- `Instance.Lifetime` - The session's `IHostApplicationLifetime` (also resolvable from the container)
- `Resolver` - The `ITypeInjectorResolver` used for injection (swap for a generated one)

### Service Registration Extensions

- `AddMonoBehaviourSingleton<T>()` - Register MonoBehaviour as singleton
- `AddKeyedMonoBehaviourSingleton<T>(key)` - Register keyed MonoBehaviour singleton
- And more overloads for interface-implementation patterns and transient MonoBehaviours

### Interfaces

- `IPostInitializationCallbacks` - Receive callback after injection
- `IAllowsReinitialization` - Allow multiple injections
- `ITypeInjectorResolver` - Custom injection logic

## Performance Notes

- The library uses reflection by default; the bundled [source generator](#source-generated-injection) removes it
  for every type a generated context covers

## License

This project is licensed under the MIT license with additional terms regarding AI usage. See the [LICENSE](./LICENSE) file.
