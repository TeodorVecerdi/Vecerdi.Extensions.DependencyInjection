---
title: Lifecycle
description: When the container is built, when hosted services run, what IHostApplicationLifetime reports, and how teardown works.
order: 4
tags: [lifecycle, hosted-services]
sources:
  - ServiceManager.cs
  - Internal/HostApplicationLifetime.cs
---

# Lifecycle

The container lives for one play session: it is built before the first scene loads and torn down after the last object
has shut down. In a player that is the whole run; in the editor, every play-mode session gets a fresh container, so
re-entering play mode without a domain reload works.

```
SubsystemRegistration   your RegisterServices / RegisterConfiguration calls queue up
BeforeSplashScreen      the provider is built, post-initialization actions run,
                        hosted services start on the thread pool
   (splash screen)      hosted services run while Unity paints the splash
                        → ApplicationStarted once every hosted service has started
Awake                   scene components are injected
quit / leaving play     → ApplicationStopping (every object still alive)
                        every OnDisable / OnDestroy runs; services still usable
[OnExitingPlayMode]     hosted services stop (reverse order) → ApplicationStopped,
                        then the provider and the configuration are disposed
```

## Startup

`ServiceManager.Instance` is the built provider; it throws before `BeforeSplashScreen`, so don't resolve services from
`SubsystemRegistration` or earlier hooks. For one-off wiring that needs resolved services but no scene, queue a
post-initialization action: it runs on the main thread right after the provider is built, before hosted services
start.

```csharp
ServiceManager.RegisterPostInitializationAction(services => {
    // Hand a container service to static code that can't be injected.
    GameLog.Initialize(services.GetRequiredService<ILoggerFactory>());
});
```

Its counterparts run at teardown: `RegisterPreTeardownAction` while the provider is still fully usable, and
`RegisterPostTeardownAction` after it is disposed, to point statics back at their standalone defaults.

## Hosted services

Every registered `IHostedService` is started concurrently on the thread pool during the splash screen, and stopped in
reverse registration order at teardown. A service that fails to start is logged and the rest carry on.

- **Off the main thread.** A hosted service's constructor and the synchronous part of `StartAsync` must not touch
  `MonoBehaviour` singletons or main-thread-only Unity APIs (`GameObject` creation, `Application.persistentDataPath`,
  …). Switch to the main thread explicitly where one is needed.
- **No scene yet.** Scene-placed singletons can't be resolved during startup; see
  [MonoBehaviour services](monobehaviour-services.md).
- **Startup order is not guaranteed.** Services that depend on each other should model the dependency instead of
  relying on registration order.

`StartAsync` receives the `ApplicationStopping` token. `StopAsync` receives a token that cancels when the shutdown
deadline (5 seconds) passes; the main thread is blocked while services stop, so a `StopAsync` that needs the main
thread to finish can't.

## IHostApplicationLifetime

The session's `IHostApplicationLifetime` is registered in the container (and available as
`ServiceManager.Instance.Lifetime`):

| Member | Fires when | Callbacks run on |
|---|---|---|
| `ApplicationStarted` | Every hosted service has finished starting (failures included). Skipped if the session is already stopping, so a waiter should also watch `ApplicationStopping` | Thread pool |
| `ApplicationStopping` | The session is ending; every object is still alive | Main thread |
| `ApplicationStopped` | Hosted services have stopped (or timed out); the provider is about to be disposed | Main thread, during teardown |
| `StopApplication()` | Asks Unity to end the session: `Application.Quit()` in a player (vetoable through `wantsToQuit`), leaving play mode in the editor | — |

`ApplicationStopping` comes from `Application.quitting`, which fires for a player quit and for leaving play mode.
Closing the editor while playing skips `Application.quitting`; the [code lifetime](code-lifetime.md) supplies the
signal there.

## Teardown

Teardown runs from Unity's `[OnExitingPlayMode]` hook, which comes after every `OnDisable` and `OnDestroy` on each path
that ends a session: leaving play mode, a player quitting, closing the editor while playing, and recompiling during
play. Every component can therefore use its injected services while it shuts down.

The order of `OnDestroy` calls on quit is arbitrary, whatever the script execution order, so don't rely on any
component outliving another. Shutdown work that must happen in a defined order belongs in
`IHostedService.StopAsync`.

> [!NOTE]
> Recompiling during play (with "Recompile And Continue Playing") tears the container down while objects stay alive,
> and nothing builds a new one until the next play session.

## See also

- [Code lifetime](code-lifetime.md): shutdown beyond the play session (code reloads, editor quit).
- [MonoBehaviour services](monobehaviour-services.md): singletons and the first scene.
