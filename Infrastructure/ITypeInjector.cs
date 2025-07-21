namespace Vecerdi.Extensions.DependencyInjection.Infrastructure;

public interface ITypeInjector {
    void Inject(IServiceProvider serviceProvider, object instance);
}
