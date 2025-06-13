namespace Vecerdi.Extensions.DependencyInjection;

public interface IPostInitializationCallbacks {
    void OnServicesInitialized();
}
