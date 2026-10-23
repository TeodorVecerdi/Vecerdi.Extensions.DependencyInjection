using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Vecerdi.Extensions.DependencyInjection.Analyzers.Tests;

/// <summary>The generated source (every context's file joined; null when nothing was generated) and the diagnostics.</summary>
public sealed record GeneratorRun(string? Generated, ImmutableArray<Diagnostic> GeneratorDiagnostics, ImmutableArray<Diagnostic> CompilationErrors) {
    public string GeneratedOrFail => Generated ?? throw new AssertFailedException("The generator produced no source.");

    public IEnumerable<string> GeneratorDiagnosticIds => GeneratorDiagnostics.Select(d => d.Id);
}

/// <summary>
/// Runs <see cref="DependencyInjectionSourceGenerator"/> over a test's source. The compilation references the real DI
/// API (<c>Vecerdi.Extensions.DependencyInjection.API</c>: the <c>[Inject*]</c> attributes and the type-injector
/// infrastructure, compiled from the library's own sources) and <c>Microsoft.Extensions.DependencyInjection.Abstractions</c>,
/// which the generated code calls. Only the two Unity types are declared here, because they need Unity; the generator
/// matches them by name and base type alone.
/// </summary>
public static class GeneratorHarness {
    public const string UnityShapes = /*lang=csharp*/
        """
        namespace UnityEngine
        {
            public class MonoBehaviour { }
        }

        namespace Vecerdi.Extensions.DependencyInjection
        {
            public abstract class BaseMonoBehaviour : UnityEngine.MonoBehaviour { }
        }
        """;

    // Everything the test host itself loads: the BCL, the DI API, DI.Abstractions, JetBrains.Annotations.
    private static readonly ImmutableArray<MetadataReference> s_References = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
        .ToImmutableArray();

    public static GeneratorRun Run(string source) {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var compilation = CSharpCompilation.Create(
            "GeneratorTests",
            [CSharpSyntaxTree.ParseText(UnityShapes, parseOptions), CSharpSyntaxTree.ParseText(source, parseOptions)],
            s_References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        // Catch mistakes in the test source itself. A context's missing GetTypeInjector (CS0534) is expected: supplying
        // it is the generator's job.
        var inputErrors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error && d.Id != "CS0534").ToList();
        if (inputErrors.Count > 0) {
            throw new AssertFailedException($"The test source does not compile:\n{string.Join("\n", inputErrors)}");
        }

        var driver = CSharpGeneratorDriver.Create(new DependencyInjectionSourceGenerator()).WithUpdatedParseOptions(parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);

        // One file per context; null when nothing was generated.
        var trees = driver.GetRunResult().GeneratedTrees;
        var generated = trees.IsEmpty ? null : string.Join("\n", trees.Select(tree => tree.ToString()));
        var errors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();
        return new GeneratorRun(generated, generatorDiagnostics, errors);
    }

    /// <summary>Runs the generator and asserts the generated code compiles against the real DI API.</summary>
    public static GeneratorRun RunAndCompile(string source) {
        var run = Run(source);
        if (run.CompilationErrors.Length > 0) {
            Assert.Fail($"The generated code does not compile:\n{string.Join("\n", run.CompilationErrors)}\n\n{run.Generated}");
        }

        return run;
    }
}
