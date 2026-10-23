using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Vecerdi.Extensions.DependencyInjection.SourceGenerator.Tests;

/// <summary>
/// Runs <see cref="MonoBehaviourSingletonFactoryAnalyzer"/> and its code fix over a test's source, compiled against
/// <c>Microsoft.Extensions.DependencyInjection.Abstractions</c> and shapes of the package's MonoBehaviour API (which
/// needs Unity, so it is declared here; the analyzer matches <c>MonoSingleton&lt;T&gt;</c> by name and namespace).
/// </summary>
public static class AnalyzerHarness {
    public const string PackageShapes = /*lang=csharp*/
        """
        namespace UnityEngine
        {
            public class MonoBehaviour { }
        }

        namespace Vecerdi.Extensions.DependencyInjection
        {
            using System;
            using Microsoft.Extensions.DependencyInjection;

            public abstract class BaseMonoBehaviour : UnityEngine.MonoBehaviour { }

            public abstract class MonoSingleton<T> : BaseMonoBehaviour where T : UnityEngine.MonoBehaviour { }

            public static class MonoBehaviourServiceCollectionExtensions
            {
                public static IServiceCollection AddMonoBehaviourSingleton<T>(this IServiceCollection services) where T : UnityEngine.MonoBehaviour => services;
                public static IServiceCollection AddMonoBehaviourSingleton<TService, TSingleton>(this IServiceCollection services) where TSingleton : UnityEngine.MonoBehaviour, TService => services;
                public static IServiceCollection AddMonoBehaviourSingleton<TService, TSingleton>(this IServiceCollection services, Func<TSingleton, TService> selector) where TSingleton : UnityEngine.MonoBehaviour where TService : notnull => services;
                public static IServiceCollection AddKeyedMonoBehaviourSingleton<TService, TSingleton>(this IServiceCollection services, object? serviceKey) where TSingleton : UnityEngine.MonoBehaviour, TService => services;
                public static IServiceCollection AddKeyedMonoBehaviourSingleton<TService, TSingleton>(this IServiceCollection services, object? serviceKey, Func<TSingleton, object?, TService> selector) where TSingleton : UnityEngine.MonoBehaviour where TService : notnull => services;
            }
        }
        """;

    private static readonly ImmutableArray<MetadataReference> s_References = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
        .ToImmutableArray();

    private static readonly CSharpParseOptions s_ParseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

    /// <summary>The analyzer's diagnostics for <paramref name="source"/>, after asserting the source compiles.</summary>
    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source) {
        var document = CreateDocument(source);
        var compilation = (await document.Project.GetCompilationAsync().ConfigureAwait(false))!;
        AssertCompiles(compilation, "The test source");
        return await compilation.WithAnalyzers([new MonoBehaviourSingletonFactoryAnalyzer()])
            .GetAnalyzerDiagnosticsAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Applies the code fix to the single diagnostic in <paramref name="source"/> and returns the fixed source, after
    /// asserting it compiles and reports nothing; null when the diagnostic offers no fix.
    /// </summary>
    public static async Task<string?> ApplyFixAsync(string source) {
        var document = CreateDocument(source);
        var compilation = (await document.Project.GetCompilationAsync().ConfigureAwait(false))!;
        AssertCompiles(compilation, "The test source");
        var diagnostics = await compilation.WithAnalyzers([new MonoBehaviourSingletonFactoryAnalyzer()]).GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
        Assert.HasCount(1, diagnostics);

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostics[0], (action, _) => actions.Add(action), CancellationToken.None);
        await new MonoBehaviourSingletonFactoryCodeFixProvider().RegisterCodeFixesAsync(context).ConfigureAwait(false);
        if (actions.Count == 0) return null;

        var operations = await actions[0].GetOperationsAsync(CancellationToken.None).ConfigureAwait(false);
        var fixedDocument = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var fixedCompilation = (await fixedDocument.Project.GetCompilationAsync().ConfigureAwait(false))!;
        AssertCompiles(fixedCompilation, "The fixed source");
        var remaining = await fixedCompilation.WithAnalyzers([new MonoBehaviourSingletonFactoryAnalyzer()]).GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
        Assert.IsEmpty(remaining, $"The fixed source still reports:\n{string.Join("\n", remaining)}");

        return (await fixedDocument.GetTextAsync().ConfigureAwait(false)).ToString();
    }

    private static Document CreateDocument(string source) {
        var workspace = new AdhocWorkspace();
        var project = workspace.CurrentSolution
            .AddProject("AnalyzerTests", "AnalyzerTests", LanguageNames.CSharp)
            .WithParseOptions(s_ParseOptions)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable))
            .WithMetadataReferences(s_References)
            .AddDocument("PackageShapes.cs", PackageShapes).Project;
        return project.AddDocument("Test.cs", source);
    }

    private static void AssertCompiles(Compilation compilation, string what) {
        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count > 0) {
            throw new AssertFailedException($"{what} does not compile:\n{string.Join("\n", errors)}");
        }
    }
}
