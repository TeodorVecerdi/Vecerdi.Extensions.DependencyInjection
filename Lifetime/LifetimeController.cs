using System.Diagnostics;
using System.Text;
using Debug = UnityEngine.Debug;

namespace Vecerdi.Extensions.Lifetime;

/// <summary>
/// One code lifetime: the stopping token, the stop callbacks, and the tracked work and threads. <see cref="CodeLifetime"/>
/// holds the process's current instance and drives it from Unity's hooks; tests create their own, so they never stop the
/// lifetime the editor itself runs on.
/// </summary>
internal sealed class LifetimeController {
    // A stop callback slower than this is reported: callbacks run on the reload/quit critical path.
    private static readonly TimeSpan s_SlowCallbackThreshold = TimeSpan.FromMilliseconds(100);

    private readonly object m_Gate = new();
    private readonly CancellationTokenSource m_Cts = new();
    private readonly List<Registration> m_Callbacks = [];
    private readonly List<(string Owner, Task Work)> m_Work = [];
    private readonly List<(string Owner, Thread Thread)> m_Threads = [];
    private readonly TimeSpan m_Budget;
    private CodeLifetimeEnd? m_Ending;

    public LifetimeController(TimeSpan budget) {
        m_Budget = budget;
    }

    public CancellationToken Token => m_Cts.Token;

    public CodeLifetimeEnd? Ending {
        get {
            lock (m_Gate) return m_Ending;
        }
    }

    public IDisposable Register(string owner, Action<CodeLifetimeEnd> stop) {
        if (owner is null) throw new ArgumentNullException(nameof(owner));
        if (stop is null) throw new ArgumentNullException(nameof(stop));

        var registration = new Registration(this, owner, stop);
        CodeLifetimeEnd ending;
        lock (m_Gate) {
            if (m_Ending is null) {
                m_Callbacks.Add(registration);
                return registration;
            }

            ending = m_Ending.Value;
        }

        // Already stopped: run it now, like CancellationToken.Register on a cancelled token.
        Invoke(registration, ending);
        return registration;
    }

    public void Track(string owner, Task work) {
        if (owner is null) throw new ArgumentNullException(nameof(owner));
        if (work is null) throw new ArgumentNullException(nameof(work));
        if (work.IsCompleted) return;

        lock (m_Gate) {
            m_Work.RemoveAll(static entry => entry.Work.IsCompleted);
            m_Work.Add((owner, work));
        }
    }

    public void Track(string owner, Thread thread) {
        if (owner is null) throw new ArgumentNullException(nameof(owner));
        if (thread is null) throw new ArgumentNullException(nameof(thread));

        lock (m_Gate) {
            m_Threads.RemoveAll(static entry => HasExited(entry.Thread));
            m_Threads.Add((owner, thread));
        }
    }

    /// <summary>
    /// Cancels the token, runs the stop callbacks in reverse registration order, then waits for tracked work and
    /// threads until the budget (counted from the start of the stop) runs out. Only the first call does anything.
    /// </summary>
    public void Stop(CodeLifetimeEnd ending) {
        Registration[] callbacks;
        lock (m_Gate) {
            if (m_Ending is not null) return;
            m_Ending = ending;
            callbacks = m_Callbacks.ToArray();
            m_Callbacks.Clear();
        }

        var start = Stopwatch.GetTimestamp();

        // Token first: loops start winding down while the callbacks run.
        try {
            m_Cts.Cancel();
        } catch (AggregateException exception) {
            foreach (var inner in exception.InnerExceptions) {
                Debug.LogException(inner);
            }
        }

        for (var i = callbacks.Length - 1; i >= 0; i--) {
            Invoke(callbacks[i], ending);
        }

        WaitForOwners(start);
    }

    /// <summary>Warns about tracked work and threads that are still running; returns how many there were.</summary>
    public int ReportStragglers(string when) {
        var stragglers = GetStragglers();
        if (stragglers.Count == 0) return 0;

        var message = new StringBuilder($"[{nameof(CodeLifetime)}] {when}, {stragglers.Count} owner(s) still running; on CoreCLR they keep this code load alive:");
        foreach (var straggler in stragglers) {
            message.Append("\n  - ").Append(straggler);
        }

        Debug.LogWarning(message.ToString());
        return stragglers.Count;
    }

    internal List<string> GetStragglers() {
        var stragglers = new List<string>();
        lock (m_Gate) {
            foreach (var (owner, work) in m_Work) {
                if (!work.IsCompleted) stragglers.Add($"'{owner}' (task)");
            }

            foreach (var (owner, thread) in m_Threads) {
                if (!HasExited(thread)) stragglers.Add($"'{owner}' (thread '{thread.Name}')");
            }
        }

        return stragglers;
    }

    private void WaitForOwners(long start) {
        (string Owner, Task Work)[] work;
        (string Owner, Thread Thread)[] threads;
        lock (m_Gate) {
            work = m_Work.ToArray();
            threads = m_Threads.ToArray();
        }

        foreach (var (owner, task) in work) {
            if (!TryGetRemaining(start, out var remaining)) return;
            try {
                task.Wait(remaining);
            } catch (AggregateException exception) {
                Debug.LogWarning($"[{nameof(CodeLifetime)}] Work tracked by '{owner}' failed: {exception.GetBaseException()}");
            }
        }

        foreach (var (_, thread) in threads) {
            if (thread == Thread.CurrentThread || IsUnstarted(thread)) continue;
            if (!TryGetRemaining(start, out var remaining)) return;
            thread.Join(remaining);
        }
    }

    private bool TryGetRemaining(long start, out TimeSpan remaining) {
        var elapsed = TimeSpan.FromSeconds((Stopwatch.GetTimestamp() - start) / (double)Stopwatch.Frequency);
        remaining = m_Budget - elapsed;
        return remaining > TimeSpan.Zero;
    }

    private static bool IsUnstarted(Thread thread) => (thread.ThreadState & System.Threading.ThreadState.Unstarted) != 0;

    private static bool HasExited(Thread thread) => !thread.IsAlive && !IsUnstarted(thread);

    private static void Invoke(Registration registration, CodeLifetimeEnd ending) {
        var start = Stopwatch.GetTimestamp();
        try {
            registration.Stop(ending);
        } catch (Exception exception) {
            Debug.LogException(exception);
        }

        var elapsed = TimeSpan.FromSeconds((Stopwatch.GetTimestamp() - start) / (double)Stopwatch.Frequency);
        if (elapsed > s_SlowCallbackThreshold) {
            Debug.LogWarning($"[{nameof(CodeLifetime)}] '{registration.Owner}' took {elapsed.TotalMilliseconds:0} ms to stop ({ending}).");
        }
    }

    private void Unregister(Registration registration) {
        lock (m_Gate) {
            m_Callbacks.Remove(registration);
        }
    }

    private sealed class Registration(LifetimeController controller, string owner, Action<CodeLifetimeEnd> stop) : IDisposable {
        public string Owner { get; } = owner;
        public Action<CodeLifetimeEnd> Stop { get; } = stop;

        public void Dispose() => controller.Unregister(this);
    }
}
