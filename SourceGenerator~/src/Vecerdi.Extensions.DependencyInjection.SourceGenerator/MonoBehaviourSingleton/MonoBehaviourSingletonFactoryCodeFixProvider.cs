using System.Collections.Immutable;
using System.Composition;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Vecerdi.Extensions.DependencyInjection.SourceGenerator;

/// <summary>
/// Rewrites a VDI0008 registration onto the <c>AddMonoBehaviourSingleton</c> family: a factory that returns the
/// singleton becomes <c>AddMonoBehaviourSingleton&lt;TService, T&gt;()</c>, one that returns something built from it
/// alone becomes the selector overload. A factory that resolves anything else gets no fix.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MonoBehaviourSingletonFactoryCodeFixProvider)), Shared]
public sealed class MonoBehaviourSingletonFactoryCodeFixProvider : CodeFixProvider {
    private const string Title = "Register with AddMonoBehaviourSingleton";
    private const string PackageNamespace = "Vecerdi.Extensions.DependencyInjection";

    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticDescriptors.CachedMonoBehaviourSingleton.Id);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context) {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
            return;

        foreach (var diagnostic in context.Diagnostics) {
            var invocation = root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<InvocationExpressionSyntax>();
            if (invocation is null || FindFixable(semanticModel, invocation, context.CancellationToken) is null)
                continue;

            context.RegisterCodeFix(
                CodeAction.Create(Title, cancellationToken => FixAsync(context.Document, invocation, cancellationToken), nameof(MonoBehaviourSingletonFactoryCodeFixProvider)),
                diagnostic);
        }
    }

    private static MonoBehaviourSingletonFactoryFinding? FindFixable(SemanticModel semanticModel, InvocationExpressionSyntax invocation, CancellationToken cancellationToken) {
        return semanticModel.GetOperation(invocation, cancellationToken) is IInvocationOperation registration
            && MonoBehaviourSingletonFactory.Analyze(registration, semanticModel.Compilation) is { Fix: not MonoBehaviourSingletonFactoryFix.None } finding
            ? finding
            : null;
    }

    private static async Task<Document> FixAsync(Document document, InvocationExpressionSyntax invocation, CancellationToken cancellationToken) {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is not CompilationUnitSyntax compilationUnit || semanticModel is null)
            return document;
        if (FindFixable(semanticModel, invocation, cancellationToken) is not { } finding)
            return document;

        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var serviceType = TypeText(TypeArgumentSyntax(invocation), finding.ServiceType!, semanticModel, invocation.SpanStart);
        var singletonType = TypeText(TypeArgumentSyntax(finding.Resolve.Syntax), finding.Singleton, semanticModel, invocation.SpanStart);
        var serviceKey = finding.IsKeyed ? MonoBehaviourSingletonFactory.GetServiceKeyArgument(finding.Registration)!.Value.Syntax.ToString() : null;

        string name;
        var arguments = new List<string>();
        if (serviceKey is not null) arguments.Add(serviceKey);

        if (finding.Fix is MonoBehaviourSingletonFactoryFix.Register) {
            name = finding.IsKeyed
                ? $"AddKeyedMonoBehaviourSingleton<{serviceType}, {singletonType}>"
                : SymbolEqualityComparer.Default.Equals(finding.ServiceType, finding.Singleton)
                    ? $"AddMonoBehaviourSingleton<{singletonType}>"
                    : $"AddMonoBehaviourSingleton<{serviceType}, {singletonType}>";
        } else {
            name = finding.IsKeyed
                ? $"AddKeyedMonoBehaviourSingleton<{serviceType}, {singletonType}>"
                : $"AddMonoBehaviourSingleton<{serviceType}, {singletonType}>";
            arguments.Add(BuildSelector(finding, semanticModel));
        }

        var newName = ((SimpleNameSyntax)SyntaxFactory.ParseTypeName(name)).WithTriviaFrom(memberAccess.Name);
        var newArguments = SyntaxFactory.ParseArgumentList($"({string.Join(", ", arguments)})").WithTriviaFrom(invocation.ArgumentList);
        var newInvocation = invocation.WithExpression(memberAccess.WithName(newName)).WithArgumentList(newArguments);

        var newRoot = compilationUnit.ReplaceNode(invocation, newInvocation);
        if (!IsImported(semanticModel.Compilation, compilationUnit)) {
            newRoot = AddUsing(newRoot);
        }

        return document.WithSyntaxRoot(newRoot);
    }

    // The selector takes the singleton where the factory resolved it; the rest of the factory's body is kept as written.
    private static string BuildSelector(MonoBehaviourSingletonFactoryFinding finding, SemanticModel semanticModel) {
        var lambda = (AnonymousFunctionExpressionSyntax)finding.Factory.Syntax;
        var keyParameter = finding.IsKeyed && finding.Factory.Symbol.Parameters.Length > 1 ? finding.Factory.Symbol.Parameters[1].Name : null;
        var parameter = UniqueParameterName(finding, semanticModel, lambda, keyParameter);
        var body = lambda.Body.ReplaceNode(finding.Resolve.Syntax, SyntaxFactory.IdentifierName(parameter).WithTriviaFrom(finding.Resolve.Syntax));
        var parameters = keyParameter is null ? parameter : $"({parameter}, {keyParameter})";
        return $"{parameters} => {body.ToFullString().Trim()}";
    }

    private static string UniqueParameterName(MonoBehaviourSingletonFactoryFinding finding, SemanticModel semanticModel, AnonymousFunctionExpressionSyntax lambda, string? keyParameter) {
        var typeName = finding.Singleton.Name;
        var baseName = char.ToLowerInvariant(typeName[0]) + typeName.Substring(1);
        var used = new HashSet<string>(lambda.Body.DescendantTokens().Where(token => token.IsKind(SyntaxKind.IdentifierToken)).Select(token => token.ValueText), StringComparer.Ordinal);
        if (keyParameter is not null) used.Add(keyParameter);

        var candidate = baseName;
        for (var suffix = 2; used.Contains(candidate) || semanticModel.LookupSymbols(lambda.SpanStart, name: candidate).Length > 0; suffix++) {
            candidate = baseName + suffix.ToString(CultureInfo.InvariantCulture);
        }

        return candidate;
    }

    // The type as the user wrote it where possible (keeps nullable annotations and aliases), its minimal name otherwise.
    private static string TypeText(TypeSyntax? written, ITypeSymbol type, SemanticModel semanticModel, int position) {
        return written?.ToString() ?? type.ToMinimalDisplayString(semanticModel, position);
    }

    private static TypeSyntax? TypeArgumentSyntax(SyntaxNode invocation) {
        return invocation is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name: GenericNameSyntax { TypeArgumentList.Arguments: { Count: > 0 } arguments } } }
            ? arguments[0]
            : null;
    }

    private static bool IsImported(Compilation compilation, CompilationUnitSyntax compilationUnit) {
        if (compilationUnit.Usings.Any(Imports))
            return true;
        if (compilationUnit.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().Any(ns => ns.Usings.Any(Imports) || ns.Name.ToString().StartsWith(PackageNamespace, StringComparison.Ordinal)))
            return true;
        return compilation.SyntaxTrees
            .Select(tree => tree.GetRoot())
            .OfType<CompilationUnitSyntax>()
            .Any(unit => unit.Usings.Any(directive => !directive.GlobalKeyword.IsKind(SyntaxKind.None) && Imports(directive)));

        static bool Imports(UsingDirectiveSyntax directive) => directive.Alias is null && directive.Name.ToString() == PackageNamespace;
    }

    private static CompilationUnitSyntax AddUsing(CompilationUnitSyntax compilationUnit) {
        var usings = compilationUnit.Usings;
        var newLine = usings.Count > 0 ? usings[0].GetTrailingTrivia() : SyntaxFactory.TriviaList(SyntaxFactory.ElasticCarriageReturnLineFeed);
        var directive = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(PackageNamespace)).WithTrailingTrivia(newLine);
        var index = 0;
        while (index < usings.Count && string.CompareOrdinal(usings[index].Name.ToString(), PackageNamespace) < 0)
            index++;
        return compilationUnit.WithUsings(usings.Insert(index, directive));
    }
}
