using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Vecerdi.Extensions.DependencyInjection.Analyzers;

/// <summary>
/// The analysis behind VDI0008, shared by the analyzer and its code fix so the two can never disagree about what a
/// registration is.
/// </summary>
internal static class MonoBehaviourSingletonFactory {
    public const string MonoSingletonMetadataName = "Vecerdi.Extensions.DependencyInjection.MonoSingleton`1";

    private const string ServiceCollectionNamespace = "Microsoft.Extensions.DependencyInjection";
    private const string PackageNamespace = "Vecerdi.Extensions.DependencyInjection";

    public static MonoBehaviourSingletonFactoryFinding? Analyze(IInvocationOperation registration, Compilation compilation) {
        var method = registration.TargetMethod;
        if (!method.IsExtensionMethod || !TryGetLifetime(method.Name, out var lifetime, out var isKeyed))
            return null;
        if (!method.ContainingNamespace.ToDisplayString().StartsWith(ServiceCollectionNamespace, StringComparison.Ordinal))
            return null;

        var factory = registration.Arguments
            .Select(argument => Unwrap(argument.Value))
            .OfType<IDelegateCreationOperation>()
            .Select(creation => creation.Target)
            .OfType<IAnonymousFunctionOperation>()
            .FirstOrDefault(lambda => lambda.Symbol.Parameters.Length > 0 && IsServiceProvider(lambda.Symbol.Parameters[0].Type));
        if (factory is null)
            return null;

        var resolves = factory.Body.Descendants().OfType<IInvocationOperation>().Where(IsResolve).ToList();
        var resolve = resolves.FirstOrDefault(invocation => invocation.TargetMethod.IsGenericMethod && IsMonoSingleton(invocation.TargetMethod.TypeArguments[0]));
        if (resolve is null)
            return null;

        var singleton = resolve.TargetMethod.TypeArguments[0];
        var serviceType = method.IsGenericMethod ? method.TypeArguments[0] : null;
        var fix = Classify(registration, factory, resolves, resolve, singleton, serviceType, isKeyed, compilation);
        return new MonoBehaviourSingletonFactoryFinding(registration, factory, resolve, singleton, lifetime, isKeyed, serviceType, fix);
    }

    public static Location GetLocation(IInvocationOperation registration) {
        return registration.Syntax is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess }
            ? memberAccess.Name.GetLocation()
            : registration.Syntax.GetLocation();
    }

    /// <summary>The <c>serviceKey</c> argument of a keyed registration.</summary>
    public static IArgumentOperation? GetServiceKeyArgument(IInvocationOperation registration) {
        return registration.Arguments.FirstOrDefault(argument => argument.Parameter?.Name == "serviceKey");
    }

    private static MonoBehaviourSingletonFactoryFix Classify(
        IInvocationOperation registration,
        IAnonymousFunctionOperation factory,
        List<IInvocationOperation> resolves,
        IInvocationOperation resolve,
        ITypeSymbol singleton,
        ITypeSymbol? serviceType,
        bool isKeyed,
        Compilation compilation
    ) {
        // The rewrite keeps the receiver and needs the service type spelled out, so it only applies to an extension
        // call in its reduced form (services.AddSingleton<TService>(...)), not to a static call or a Type overload.
        if (serviceType is null)
            return MonoBehaviourSingletonFactoryFix.None;
        if (registration.Syntax is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax })
            return MonoBehaviourSingletonFactoryFix.None;
        if (registration.Arguments.Length == 0 || registration.Arguments[0].Syntax is ArgumentSyntax)
            return MonoBehaviourSingletonFactoryFix.None;
        if (isKeyed && GetServiceKeyArgument(registration) is null)
            return MonoBehaviourSingletonFactoryFix.None;
        // The factory may use the provider for exactly one thing: resolving this singleton.
        if (resolves.Count != 1 || resolve.TargetMethod.Name is not ("GetService" or "GetRequiredService"))
            return MonoBehaviourSingletonFactoryFix.None;

        var provider = factory.Symbol.Parameters[0];
        var providerReferences = factory.Body.Descendants()
            .OfType<IParameterReferenceOperation>()
            .Where(reference => SymbolEqualityComparer.Default.Equals(reference.Parameter, provider))
            .ToList();
        if (providerReferences.Count != 1 || !IsWithin(providerReferences[0], resolve))
            return MonoBehaviourSingletonFactoryFix.None;

        if (ReturnsOnly(factory, resolve)) {
            var conversion = compilation.ClassifyCommonConversion(singleton, serviceType);
            return conversion.IsImplicit && (conversion.IsIdentity || conversion.IsReference)
                ? MonoBehaviourSingletonFactoryFix.Register
                : MonoBehaviourSingletonFactoryFix.None;
        }

        return MonoBehaviourSingletonFactoryFix.Select;
    }

    private static bool ReturnsOnly(IAnonymousFunctionOperation factory, IInvocationOperation resolve) {
        return factory.Body.Operations.Length == 1
            && factory.Body.Operations[0] is IReturnOperation { ReturnedValue: { } returned }
            && ReferenceEquals(Unwrap(returned), resolve);
    }

    private static bool IsWithin(IOperation operation, IOperation ancestor) {
        for (var current = operation.Parent; current is not null; current = current.Parent) {
            if (ReferenceEquals(current, ancestor)) return true;
        }

        return false;
    }

    private static bool TryGetLifetime(string name, out string lifetime, out bool isKeyed) {
        (lifetime, isKeyed) = name switch {
            "AddSingleton" or "TryAddSingleton" => ("singleton", false),
            "AddScoped" or "TryAddScoped" => ("scoped", false),
            "AddKeyedSingleton" or "TryAddKeyedSingleton" => ("singleton", true),
            "AddKeyedScoped" or "TryAddKeyedScoped" => ("scoped", true),
            _ => ("", false),
        };
        return lifetime.Length > 0;
    }

    private static bool IsResolve(IInvocationOperation invocation) {
        var method = invocation.TargetMethod;
        if (method.Name is not ("GetService" or "GetRequiredService" or "GetKeyedService" or "GetRequiredKeyedService"))
            return false;
        return method.IsExtensionMethod
            ? method.Parameters.Length > 0 && IsServiceProvider(method.Parameters[0].Type)
            : method.ContainingType is { } type && (IsServiceProvider(type) || type.AllInterfaces.Any(IsServiceProvider));
    }

    private static bool IsServiceProvider(ITypeSymbol type) {
        return type is { Name: "IServiceProvider", ContainingNamespace.Name: "System" }
            || type.AllInterfaces.Any(i => i is { Name: "IServiceProvider", ContainingNamespace.Name: "System" });
    }

    private static bool IsMonoSingleton(ITypeSymbol type) {
        for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType) {
            if (current.OriginalDefinition is { Name: "MonoSingleton", Arity: 1 } definition && definition.ContainingNamespace.ToDisplayString() == PackageNamespace) {
                return true;
            }
        }

        return false;
    }

    private static IOperation Unwrap(IOperation operation) {
        while (operation is IConversionOperation { IsImplicit: true } conversion)
            operation = conversion.Operand;
        return operation;
    }
}
