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
/// Injection happens on the main thread only (from <c>Awake</c> or an explicit <c>InjectServices()</c> call), so
/// the tracker is deliberately not thread-safe.
/// </para>
/// </remarks>
internal sealed class InjectedInstancesTracker {
    private const int SweepInterval = 512;
    private readonly HashSet<EntityId> m_Instances = [];
    private int m_OperationsSinceSweep;

    public bool Contains(MonoBehaviour instance) {
        CountOperation();
        return m_Instances.Contains(instance.GetEntityId());
    }

    public void Add(MonoBehaviour instance) {
        CountOperation();
        m_Instances.Add(instance.GetEntityId());
    }

    public void Clear() {
        m_Instances.Clear();
        m_OperationsSinceSweep = 0;
    }

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
        Span<bool> valid = count <= 1024 ? stackalloc bool[count] : new bool[count];
        Resources.EntityIdsToValidArray(ids, valid);

        for (var i = 0; i < count; i++) {
            if (!valid[i]) {
                m_Instances.Remove(ids[i]);
            }
        }
    }
}
