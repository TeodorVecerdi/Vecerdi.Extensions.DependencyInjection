using Microsoft.Extensions.Hosting;
using UnityEditor;
using UnityEngine;

namespace Vecerdi.Extensions.DependencyInjection.Internal;

/// <summary>
/// <see cref="IHostApplicationLifetime"/> for one play session, driven by <see cref="ServiceManager"/>. Each notification
/// fires once; a callback that throws is logged and does not keep the others from running.
/// </summary>
internal sealed class HostApplicationLifetime(SynchronizationContext? mainThread) : IHostApplicationLifetime {
    private readonly CancellationTokenSource m_Started = new();
    private readonly CancellationTokenSource m_Stopping = new();
    private readonly CancellationTokenSource m_Stopped = new();

    /// <summary>Every hosted service has finished starting. Callbacks run on the thread pool.</summary>
    public CancellationToken ApplicationStarted => m_Started.Token;

    /// <summary>The play session is ending; every service is still alive. Callbacks run on the main thread.</summary>
    public CancellationToken ApplicationStopping => m_Stopping.Token;

    /// <summary>
    /// Hosted services have stopped (or timed out); the provider is about to be disposed. Callbacks run on the main
    /// thread during teardown, so keep them quick.
    /// </summary>
    public CancellationToken ApplicationStopped => m_Stopped.Token;

    /// <summary>
    /// Asks Unity to end the session: <c>Application.Quit()</c> in a player (which <c>wantsToQuit</c> may still veto),
    /// leaving play mode in the editor. Safe from any thread.
    /// </summary>
    public void StopApplication() {
        if (mainThread is null || SynchronizationContext.Current == mainThread) {
            RequestStop();
        } else {
            mainThread.Post(static _ => RequestStop(), null);
        }
    }

    /// <summary>Signals Started, unless the session is already stopping (a quit during startup).</summary>
    public void NotifyStarted() {
        if (m_Stopping.IsCancellationRequested) return;
        Notify(m_Started);
    }

    public void NotifyStopping() {
        Notify(m_Stopping);
    }

    public void NotifyStopped() {
        Notify(m_Stopped);
    }

    private static void RequestStop() {
#if UNITY_EDITOR
        EditorApplication.ExitPlaymode();
#else
        Application.Quit();
#endif
    }

    private static void Notify(CancellationTokenSource source) {
        if (source.IsCancellationRequested) return;
        try {
            source.Cancel();
        } catch (AggregateException exception) {
            foreach (var inner in exception.InnerExceptions) {
                Debug.LogException(inner);
            }
        }
    }
}
