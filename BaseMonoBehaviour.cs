using UnityEngine;

namespace Vecerdi.Extensions.DependencyInjection;

public abstract class BaseMonoBehaviour : MonoBehaviour {
    protected virtual void Awake() {
        this.InjectServices();
    }
}
