namespace Vecerdi.Extensions.DependencyInjection.Infrastructure;

public sealed class ReflectionTypeInjectorResolver : ITypeInjectorResolver {
    public ITypeInjector GetTypeInjector(Type type) => new ReflectionTypeInjector(type);
}
