---
title: Code lifetime
description: Vecerdi.Extensions.Lifetime - tying threads, servers, and loops to code reloads and quits.
order: 6
tags: [lifecycle, lifetime]
sources:
  - Lifetime/
---

# Code lifetime

`CodeLifetime` is the lifetime of the currently loaded code: it ends when the editor reloads code or quits, and when a
player quits. Anything that owns a thread, socket, file watcher, timer or long-running loop outside a play session
ties its shutdown to it, so nothing outlives the code that started it. On CoreCLR, that's what lets an unloaded code
generation be garbage-collected; on Mono, it keeps shutdown orderly.

It lives in its own assembly, `Vecerdi.Extensions.Lifetime` (in `Lifetime/`), with no references: reference that
asmdef without pulling in the container or any NuGet package.

> [!NOTE]
> The container's [lifecycle](lifecycle.md) covers one play session. With domain reload off, many play sessions run
> inside a single code lifetime.

## Stopping with the code

```csharp
using Vecerdi.Extensions.Lifetime;

var registration = CodeLifetime.OnStopping("ChatServer", ending => {
    if (ending == CodeLifetimeEnd.Reload) m_Server.Suspend(); else m_Server.Shutdown();
});

// Long-running loops observe the token.
while (!CodeLifetime.Stopping.IsCancellationRequested) {
    PumpMessages();
}
```

`OnStopping` callbacks run synchronously on the main thread, in reverse registration order, with the reason:
`CodeLifetimeEnd.Reload` for a code reload, `CodeLifetimeEnd.Quit` for quitting. Each runs isolated from the others'
exceptions, and one slower than 100 ms is reported by owner name. Dispose the registration when the owner shuts down
on its own first. Registering after the stop started runs the callback immediately.

## Waiting for work and threads

Work that should finish rather than be abandoned (a background file write, say) and threads that must exit can be
tracked; a stop waits for them, two seconds in total, and reports whatever is still running.

```csharp
CodeLifetime.Track("ChatServer", m_FlushTask);
CodeLifetime.Track("ChatServer", m_ReceiveThread);
```

The wait blocks the main thread, so tracked work must not need the main thread to finish, and a tracked thread must
exit by itself: observe `Stopping`, or be told to stop by an `OnStopping` callback.

## When it stops

| Situation | Signal | Reason |
|---|---|---|
| Editor quits | `EditorApplication.quitting` | `Quit` |
| Editor reloads code | `[OnCodeDeinitializing]` | `Reload` |
| Player quits | `Application.quitting` (after `wantsToQuit` approved it) | `Quit` |

Unity objects are still alive in all three. It stops once. Whatever is still running is reported at
`[OnCodeUnloading]` in the editor, and right after the stop in a player.

> [!TIP]
> Asset import workers run the same hooks. The lifetime is harmless there, but anything that starts a server should
> keep its own `AssetDatabase.IsAssetImportWorkerProcess()` check.
