using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Vecerdi.Extensions.DependencyInjection.SourceGenerator;

/// <summary>
/// VDI0008: a <c>Singleton</c> or <c>Scoped</c> factory registration whose lambda resolves a <c>MonoSingleton&lt;T&gt;</c>.
/// The container caches whatever the factory returns, so the registration keeps handing out the instance (or a part of
/// it) after Unity destroys it. A MonoBehaviour singleton is recognised by its base type alone; a plain MonoBehaviour
/// registered with <c>AddMonoBehaviourSingleton</c>, or a resolve through an interface, is not flagged.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MonoBehaviourSingletonFactoryAnalyzer : DiagnosticAnalyzer {
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(DiagnosticDescriptors.CachedMonoBehaviourSingleton);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
            // Nothing to find in an assembly that can't see MonoSingleton<T>.
            if (start.Compilation.GetTypeByMetadataName(MonoBehaviourSingletonFactory.MonoSingletonMetadataName) is null)
                return;
            start.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context) {
        var registration = (IInvocationOperation)context.Operation;
        if (MonoBehaviourSingletonFactory.Analyze(registration, context.Compilation) is not { } finding)
            return;
        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.CachedMonoBehaviourSingleton,
            MonoBehaviourSingletonFactory.GetLocation(registration),
            finding.Singleton.Name,
            finding.Lifetime));
    }
}
