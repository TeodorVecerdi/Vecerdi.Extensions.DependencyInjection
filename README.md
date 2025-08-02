# Vecerdi.Extensions.DependencyInjection

A modern dependency injection library for Unity that brings Microsoft's dependency injection container to Unity projects with seamless integration and automatic service injection.

See also [Vecerdi.Extensions.DependencyInjection.SourceGenerator](https://github.com/TeodorVecerdi/Vecerdi.Extensions.DependencyInjection.SourceGenerator) for a more performant and reflection-free solution to injecting services.

## Features

- 🎯 **Property-based injection** using attributes
- 🔑 **Keyed services** support for advanced scenarios
- 🏗️ **MonoBehaviour integration** with automatic service creation
- ⚡ **Performance optimized** with caching and optional source generation
- 🎛️ **Configuration management** using Microsoft.Extensions.Configuration
- 🔄 **Service lifecycle management** (Singleton, Transient)
- 🧩 **Modern C# features** support (up to C# 13)

## Requirements

- Unity 6 or later
- Modern C# compiler with C# 13+ support
- [UnityRoslynUpdater](https://github.com/DaZombieKiller/UnityRoslynUpdater) to enable modern C# features in Unity
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

### 3. Service Manager Setup

Add the `ServiceManager` to a GameObject in your first scene (or create it dynamically). Set its script execution order to as low value such as -10,000 to ensure its lifecycle events run before all other scripts.

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
