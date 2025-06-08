using UnityEngine;

namespace MediaBrowser.DependencyInjection.Extensions;

public static class MonoBehaviourExtensions {
    public static void InjectServices(this MonoBehaviour monoBehaviour) {
        if (monoBehaviour is ServiceManager)
            return;
        BehaviourServices.InjectIntoMonoBehaviourProperties(ServiceManager.Instance, monoBehaviour);
    }
}
