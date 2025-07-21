using System.Collections.Concurrent;
using UnityEngine;

namespace Vecerdi.Extensions.DependencyInjection.Infrastructure;

internal sealed class InjectedInstancesTracker {
    private const int CleanupInterval = 100;
    private readonly ConcurrentDictionary<MonoBehaviour, byte> m_Instances = new();
    private int m_OperationCount;

    public bool Contains(MonoBehaviour instance) {
        IncrementOperationCount();
        return m_Instances.ContainsKey(instance);
    }

    public void Add(MonoBehaviour instance) {
        IncrementOperationCount();
        m_Instances.TryAdd(instance, 0);
    }

    private void IncrementOperationCount() {
        var count = Interlocked.Increment(ref m_OperationCount);
        if (count % CleanupInterval == 0) {
            CleanupDestroyed();
        }
    }

    private void CleanupDestroyed() {
        var toRemove = m_Instances.Keys.Where(instance => instance == null).ToList();
        foreach (var instance in toRemove) {
            m_Instances.TryRemove(instance!, out _);
        }
    }
}
