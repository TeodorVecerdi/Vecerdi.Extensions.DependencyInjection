using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Vecerdi.Extensions.DependencyInjection.SourceGenerator;

/// <summary>A container-cached factory registration whose lambda resolves a MonoBehaviour singleton.</summary>
internal sealed class MonoBehaviourSingletonFactoryFinding(
    IInvocationOperation registration,
    IAnonymousFunctionOperation factory,
    IInvocationOperation resolve,
    ITypeSymbol singleton,
    string lifetime,
    bool isKeyed,
    ITypeSymbol? serviceType,
    MonoBehaviourSingletonFactoryFix fix
) {
    public IInvocationOperation Registration { get; } = registration;
    public IAnonymousFunctionOperation Factory { get; } = factory;
    public IInvocationOperation Resolve { get; } = resolve;
    public ITypeSymbol Singleton { get; } = singleton;
    public string Lifetime { get; } = lifetime;
    public bool IsKeyed { get; } = isKeyed;
    public ITypeSymbol? ServiceType { get; } = serviceType;
    public MonoBehaviourSingletonFactoryFix Fix { get; } = fix;
}
