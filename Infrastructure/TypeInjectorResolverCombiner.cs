namespace Vecerdi.Extensions.DependencyInjection.Infrastructure;

public class TypeInjectorResolverCombiner(params ITypeInjectorResolver[] resolvers) : ITypeInjectorResolver {
    public ITypeInjector? GetTypeInjector(Type type) {
        foreach (var resolver in resolvers) {
            var injector = resolver.GetTypeInjector(type);
            if (injector != null)
                return injector;
        }

        return null;
    }
}
