using MediaBrowser.DependencyInjection.Extensions;
using UnityEngine;

namespace MediaBrowser.DependencyInjection;

public abstract class BaseMonoBehaviour : MonoBehaviour {
    protected virtual void Awake() {
        this.InjectServices();
    }
}
