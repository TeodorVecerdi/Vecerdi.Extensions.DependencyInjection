using UnityEngine;

namespace Vecerdi.Extensions.DependencyInjection;

public abstract class BaseMonoBehaviour : MonoBehaviour {
    /// <summary>
    /// Whether this instance has already had its services injected. Used by the injection pipeline as a fast path
    /// in front of the id-based tracker; only meaningful for types that do not implement
    /// <see cref="IAllowsReinitialization"/>.
    /// </summary>
    internal bool ServicesInjected { get; set; }

    protected virtual void Awake() {
        this.InjectServices();
    }
}
