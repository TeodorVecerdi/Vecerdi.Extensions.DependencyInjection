# Vecerdi.Extensions.DependencyInjection

`Microsoft.Extensions.DependencyInjection` for Unity. Register services the way any .NET app does; `MonoBehaviour`s
receive them through `[Inject]` properties.

## Highlights

- **Nothing to set up in scenes.** The container builds itself before the first scene loads, so every `Awake` already
  sees it. No bootstrap object, no execution-order tricks.
- **Components as services.** Register a `MonoBehaviour` like any other service; the container finds it in the scene
  or creates one, and keeps the ones it creates alive across scene loads.
- **Hosted services and a real lifetime.** `IHostedService`s start during the splash screen, and
  `IHostApplicationLifetime` tells you when the session starts, is stopping, and has stopped.
- **Services work in `OnDestroy`.** Teardown runs after every component has shut down, on every way a session ends:
  quitting, leaving play mode, closing the editor.
- **Reflection-free when you want it.** A bundled source generator writes your injectors at compile time.
- **Clean code reloads.** `Vecerdi.Extensions.Lifetime` tells threads, servers and loops when the code they belong to
  is going away.

## Quick start

Add the repository under `Assets/` (as a git submodule, or copied), along with the NuGet packages listed in
[Getting started](Documentation~/getting-started.md#requirements). Then register your services:

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
        });
    }
}
```

…and inject them:

```csharp
using Vecerdi.Extensions.DependencyInjection;

public sealed class ScoreBoard : BaseMonoBehaviour {
    [Inject] internal IScoreService Scores { get; set; } = null!;
    [Inject] internal AudioDirector Audio { get; set; } = null!;

    private void Start() => Scores.Changed += OnScoreChanged;
}
```

That's it: `BaseMonoBehaviour` injects in `Awake`. For a component that can't change its base class, call
`this.InjectServices()` instead.

## Documentation

| Article | What it covers |
|---|---|
| [Getting started](Documentation~/getting-started.md) | Requirements, installation, registration, configuration |
| [Injection](Documentation~/injection.md) | Inject attributes, keyed services, callbacks, resolvers |
| [MonoBehaviour services](Documentation~/monobehaviour-services.md) | Components as services, `MonoSingleton<T>`, persistence |
| [Lifecycle](Documentation~/lifecycle.md) | Boot order, hosted services, `IHostApplicationLifetime`, teardown |
| [Source-generated injection](Documentation~/source-generation.md) | The bundled generator |
| [Code lifetime](Documentation~/code-lifetime.md) | `Vecerdi.Extensions.Lifetime` |

## Requirements

Unity 6.5 or later, and C# 12+ language features (Unity 6 with
[UnityRoslynUpdater](https://github.com/DaZombieKiller/UnityRoslynUpdater), or Unity 7).

## License

MIT, with additional terms regarding AI usage. See [LICENSE](LICENSE).
