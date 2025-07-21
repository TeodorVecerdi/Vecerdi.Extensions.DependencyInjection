namespace Vecerdi.Extensions.DependencyInjection.Infrastructure;

public interface ITypeInjectorResolver {
    ITypeInjector? GetTypeInjector(Type type);
}
