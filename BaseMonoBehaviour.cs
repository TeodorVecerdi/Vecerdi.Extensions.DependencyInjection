using MediaVault.DependencyInjection.Extensions;
using UnityEngine;

namespace MediaVault.DependencyInjection;

public abstract class BaseMonoBehaviour : MonoBehaviour {
    protected virtual void Awake() {
        this.InjectServices();
    }
}
