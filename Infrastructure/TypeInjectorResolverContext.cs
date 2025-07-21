namespace Vecerdi.Extensions.DependencyInjection.Infrastructure;

public abstract class TypeInjectorResolverContext : ITypeInjectorResolver {
    public abstract ITypeInjector? GetTypeInjector(Type type);
}
