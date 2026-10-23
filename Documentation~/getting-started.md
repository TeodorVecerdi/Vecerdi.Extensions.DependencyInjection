---
title: Getting started
description: Requirements, installation, and registering your first services with the package.
order: 1
tags: [getting-started, registration]
sources:
  - ServiceManager.cs
  - Vecerdi.Extensions.DependencyInjection.asmdef
---

# Getting started

The package is a thin layer over `Microsoft.Extensions.DependencyInjection`: a `ServiceManager` that builds the
container at startup, plus the glue that hands services to `MonoBehaviour`s. There is no bootstrap component to place
in a scene.

## Requirements

- **Unity 6.5 or later.** Teardown runs from Unity's lifecycle attributes (`[OnExitingPlayMode]`), which earlier
  versions lack.
- **Modern C#.** The source uses C# 12+ features: Unity 6 with
  [UnityRoslynUpdater](https://github.com/DaZombieKiller/UnityRoslynUpdater) on Mono, or Unity 7, which ships them.
- **NuGet packages** (for example through NuGetForUnity): `Microsoft.Extensions.DependencyInjection`,
  `Microsoft.Extensions.Hosting.Abstractions`, `Microsoft.Extensions.Configuration`,
  `Microsoft.Extensions.Logging.Abstractions` and `PolySharp`.

## Installation

The package is embedded rather than installed from a registry: add the repository under `Assets/` as a git submodule
(or copy it). It contains three assemblies:

| Assembly | Folder | Purpose |
|---|---|---|
| `Vecerdi.Extensions.DependencyInjection` | package root | The container, injection, `MonoBehaviour` integration |
| `Vecerdi.Extensions.Lifetime` | `Lifetime/` | [Code lifetime](code-lifetime.md); no references, usable on its own |
| `Vecerdi.Extensions.DependencyInjection.Analyzers` | `Analyzers/` | Roslyn DLL, applied automatically: the [injector generator](source-generation.md) and the [VDI0008 analyzer](monobehaviour-services.md#how-singletons-resolve) |

## Registering services

Register from a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` method. The
callbacks only queue; the container is built at `BeforeSplashScreen` from everything queued by then.

```csharp
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;
using Vecerdi.Extensions.DependencyInjection;

public static class GameServices {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Register() {
        ServiceManager.RegisterServices((services, configuration) => {
            services.AddSingleton<IScoreService, ScoreService>();
            services.AddMonoBehaviourSingleton<AudioDirector>();
            services.AddHostedService<SaveGameAutosaver>();
        });
    }
}
```

Logging is optional. With an `ILogger<T>` registered (`services.AddLogging(...)`), `ServiceManager` reports
hosted-service failures through it; without one, they go straight to the Unity console.

Split registrations across features with your own `IServiceCollection` extension methods, and call them from one
composition root.

## Configuration

`ServiceManager.RegisterConfiguration` adds sources to the `IConfigurationManager` before any registration callback
runs, so `RegisterServices` sees the complete configuration. The result is also registered as `IConfiguration`.

```csharp
ServiceManager.RegisterConfiguration(configuration => {
    configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Audio:MasterVolume"] = "0.8" });
});
```

Any `Microsoft.Extensions.Configuration` source works; add the package that provides it (JSON files, environment
variables, …) to your project.

## Next steps

- [Injection](injection.md): how `MonoBehaviour`s receive services.
- [MonoBehaviour services](monobehaviour-services.md): registering components as services.
- [Lifecycle](lifecycle.md): what runs when, and how teardown works.
