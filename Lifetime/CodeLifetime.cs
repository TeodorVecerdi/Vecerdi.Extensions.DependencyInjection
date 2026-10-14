using Unity.Scripting.LifecycleManagement;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Vecerdi.Extensions.Lifetime;

/// <summary>
/// The lifetime of the currently loaded code: it ends when the editor reloads code or quits, and when a player quits.
/// Anything that owns a thread, socket, watcher, timer or long-running loop outside a play session ties its shutdown
/// to it, so nothing outlives the code that started it. On CoreCLR that is what lets an unloaded code generation be
/// collected; on Mono it keeps shutdown orderly. The DI host's <c>IHostApplicationLifetime</c> is the play-session
/// counterpart; with domain reload off, many play sessions run inside one code lifetime.
/// </summary>
/// <remarks>
/// <para>
/// Stopping happens once. In the editor it starts from <c>EditorApplication.quitting</c> (<see cref="CodeLifetimeEnd.Quit"/>)
/// or <c>[OnCodeDeinitializing]</c> (<see cref="CodeLifetimeEnd.Reload"/>; it also fires on quit, after <c>quitting</c>),
/// both while Unity objects are still alive. In a player it starts from <c>Application.quitting</c>, which only fires once
/// <c>wantsToQuit</c> has approved the quit. The order of a stop: <see cref="Stopping"/> is cancelled, the
/// <see cref="OnStopping"/> callbacks run in reverse registration order, then tracked work and threads get what is left of
/// <see cref="StopBudget"/> to finish. Whatever is still running is reported: in the editor at <c>[OnCodeUnloading]</c>,
/// in a player right after the stop.
/// </para>
/// <para>
/// Asset import workers run the same hooks. The lifetime is harmless there, but anything that starts a server or
/// otherwise acts outside its process should keep its own <c>AssetDatabase.IsAssetImportWorkerProcess()</c> check.
/// </para>
/// </remarks>
[NoAutoStaticsCleanup]
public static partial class CodeLifetime {
    /// <summary>How long a stop waits, in total, for tracked work and threads.</summary>
    public static readonly TimeSpan StopBudget = TimeSpan.FromSeconds(2);

    private static LifetimeController s_Current = new(StopBudget);

    /// <summary>Cancelled when the code lifetime starts stopping. Long-running loops observe it.</summary>
    public static CancellationToken Stopping => Volatile.Read(ref s_Current).Token;

    /// <summary>Why the lifetime ended, or null while it is still running.</summary>
    public static CodeLifetimeEnd? Ending => Volatile.Read(ref s_Current).Ending;

    /// <summary>
    /// Runs <paramref name="stop"/> synchronously, on the thread that stops the lifetime (the main thread), with the
    /// reason. Callbacks run in reverse registration order, each isolated from the others' exceptions, and one slower than
    /// 100 ms is reported by <paramref name="owner"/>. Registering after the stop started runs the callback immediately.
    /// Dispose the result to unregister, e.g. when the owner shuts down on its own first.
    /// </summary>
    public static IDisposable OnStopping(string owner, Action<CodeLifetimeEnd> stop) => Volatile.Read(ref s_Current).Register(owner, stop);

    /// <summary>
    /// Has a stop wait (within <see cref="StopBudget"/>) for in-flight work that should finish rather than be abandoned,
    /// such as a background file write, and report it if it does not. The wait blocks the main thread: work that needs the
    /// main thread to complete cannot finish during it.
    /// </summary>
    public static void Track(string owner, Task work) => Volatile.Read(ref s_Current).Track(owner, work);

    /// <summary>
    /// Has a stop join <paramref name="thread"/> (within <see cref="StopBudget"/>) and report it if it is still alive. The
    /// thread itself has to exit: observe <see cref="Stopping"/>, or be told to stop by an <see cref="OnStopping"/> callback.
    /// </summary>
    public static void Track(string owner, Thread thread) => Volatile.Read(ref s_Current).Track(owner, thread);

    [OnCodeLoaded]
    private static void OnCodeLoaded() {
        // Statics normally start fresh with each code load. If they survived one, the old lifetime is over: start anew.
        // A lifetime that has not stopped is kept, since registrations made before this hook already live in it.
        if (Volatile.Read(ref s_Current).Ending is not null) {
            Volatile.Write(ref s_Current, new LifetimeController(StopBudget));
        }

#if UNITY_EDITOR
        EditorApplication.quitting -= OnEditorQuitting;
        EditorApplication.quitting += OnEditorQuitting;
#else
        Application.quitting -= OnApplicationQuitting;
        Application.quitting += OnApplicationQuitting;
#endif
    }

#if UNITY_EDITOR
    private static void OnEditorQuitting() => Volatile.Read(ref s_Current).Stop(CodeLifetimeEnd.Quit);

    [OnCodeDeinitializing]
    private static void OnCodeDeinitializing() => Volatile.Read(ref s_Current).Stop(CodeLifetimeEnd.Reload);

    [OnCodeUnloading]
    private static void OnCodeUnloading() => Volatile.Read(ref s_Current).ReportStragglers("At code unload");
#else
    private static void OnApplicationQuitting() {
        var current = Volatile.Read(ref s_Current);
        current.Stop(CodeLifetimeEnd.Quit);
        current.ReportStragglers("At quit");
    }
#endif
}
