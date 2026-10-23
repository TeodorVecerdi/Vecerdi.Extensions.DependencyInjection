using UnityEngine;

namespace Vecerdi.Extensions.DependencyInjection.Infrastructure;

/// <summary>
/// Tracks which <see cref="MonoBehaviour"/> instances have already had their services injected, so that the
/// double-injection path (a manual <c>InjectServices()</c> on an inactive object followed by its <c>Awake</c>)
/// can be short-circuited.
/// </summary>
/// <remarks>
/// Instances are tracked by <see cref="EntityId"/> rather than by reference: ids are unique for the session and
/// are never recycled, so a stale entry for a destroyed object is harmless, and the tracker never keeps a destroyed
/// object's managed shell (or anything it references) alive. Entries for destroyed objects are swept periodically
/// through <see cref="Resources.EntityIdsToValidArray(ReadOnlySpan{EntityId}, Span{bool})"/> purely to keep the
/// set bounded.
/// <para>
/// Injection usually runs on the main thread (<c>Awake</c>), but an explicit <c>InjectServices()</c> may come from any
/// thread, so every operation takes a private lock. It is almost never contended. Both Unity calls it makes are
/// thread-safe: <see cref="UnityEngine.Object.GetEntityId"/> reads a field, and the validity check is a thread-safe
/// native binding.
/// </para>
/// </remarks>
internal sealed class InjectedInstancesTracker {
    private const int SweepInterval = 512;
    private readonly object m_Gate = new();
    private readonly HashSet<EntityId> m_Instances = [];
    private int m_OperationsSinceSweep;

    public bool Contains(MonoBehaviour instance) {
        var entityId = instance.GetEntityId();
        lock (m_Gate) {
            CountOperation();
            return m_Instances.Contains(entityId);
        }
    }

    public void Add(MonoBehaviour instance) {
        var entityId = instance.GetEntityId();
        lock (m_Gate) {
            CountOperation();
            m_Instances.Add(entityId);
        }
    }

    public void Clear() {
        lock (m_Gate) {
            m_Instances.Clear();
            m_OperationsSinceSweep = 0;
        }
    }

    // CountOperation and SweepDestroyed run under m_Gate.

    private void CountOperation() {
        if (++m_OperationsSinceSweep < SweepInterval) return;
        m_OperationsSinceSweep = 0;
        SweepDestroyed();
    }

    private void SweepDestroyed() {
        var count = m_Instances.Count;
        if (count == 0) return;

        var ids = new EntityId[count];
        m_Instances.CopyTo(ids);
        var valid = count <= 1024 ? stackalloc bool[count] : new bool[count];
        Resources.EntityIdsToValidArray(ids, valid);

        for (var i = 0; i < count; i++) {
            if (!valid[i]) {
                m_Instances.Remove(ids[i]);
            }
        }
    }
}
