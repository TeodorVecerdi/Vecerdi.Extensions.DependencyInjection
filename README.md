# Vecerdi.Extensions.DependencyInjection

A modern dependency injection library for Unity that brings Microsoft's dependency injection container to Unity projects with seamless integration and automatic service injection.

See also [Vecerdi.Extensions.DependencyInjection.SourceGenerator](https://github.com/TeodorVecerdi/Vecerdi.Extensions.DependencyInjection.SourceGenerator) for a more performant and reflection-free solution to injecting services.

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
- The editor teardown hook uses Unity's built-in play-mode lifecycle attributes, which are not present before 6.5
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
BeforeSceneLoad         a hidden host object is spawned (DontDestroyOnLoad, execution order int.MaxValue)
Awake                   scene MonoBehaviours inject
quit / play-mode exit   every OnDisable/OnDestroy runs, then the host's OnDestroy stops the hosted services
                        (reverse order) and disposes the provider
```

- **`ServiceManager.Instance`** is the built provider. It throws before `BeforeSplashScreen`, so do not resolve
  services from `SubsystemRegistration` or `AfterAssembliesLoaded` hooks — queue a post-initialization action
  instead.
- **`ServiceManager.RegisterPostInitializationAction(Action<IServiceProvider>)`** runs synchronously on the main
  thread right after the provider is built and before hosted services start. Use it for one-off wiring that needs
  resolved services but no scene.
- **`ServiceManager.Instance.WhenReady`** is a `Task` that completes once every hosted service has finished
  starting. It never faults: startup failures are logged per service and readiness still completes.
- **Hosted services start on the thread pool.** Their constructors and the synchronous head of `StartAsync` must
  not touch MonoBehaviour singletons or main-thread-only Unity APIs (`Application.persistentDataPath`, `Screen`,
  `GameObject` creation, …). Hop to the main thread explicitly where one is needed. Services start concurrently,
  so a service that depends on another should model that dependency rather than rely on registration order.
- **Teardown** runs from the host object's `OnDestroy`. Unity destroys objects in ascending script execution order,
  and the host sits at `int.MaxValue`, so every other `OnDisable` and `OnDestroy` runs first and can still use its
  injected services. Stopping the hosted services (reverse registration order) and disposing the provider each get
  a bounded wait (5 s) so a stuck service cannot hang exit. Leaving play mode in the editor runs the same path, so
  re-entering play mode without a domain reload works. If the first scene never loads, `Application.quitting` is the
  fallback.
- **Do not give your own scripts `DefaultExecutionOrder(int.MaxValue)`.** Order among equal values is undefined,
  so such a script may be destroyed after the container is gone. Shutdown work that must run in a defined order
  belongs in `IHostedService.StopAsync`.

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

### Custom Type Injectors

For optimal performance, you can provide custom type injectors (e.g., from source generators):

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
static void SetupCustomResolver()
{
    // Replace reflection-based injection with generated code
    ServiceManager.Resolver = new GeneratedTypeInjectorResolver();
}
```

See [Vecerdi.Extensions.DependencyInjection.SourceGenerator](https://github.com/TeodorVecerdi/Vecerdi.Extensions.DependencyInjection.SourceGenerator) for a source generator that generates all required code for injecting services, removing the need for using reflection.

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
- `Instance.WhenReady` - Completes when all hosted services have started
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

- The library uses reflection by default but supports source generators for zero-reflection injection
- Use the [companion](https://github.com/TeodorVecerdi/Vecerdi.Extensions.DependencyInjection.SourceGenerator) source generator for maximum performance in production builds

## License

This project is licensed under the MIT license with additional terms regarding AI usage. See the [LICENSE](./LICENSE) file.
