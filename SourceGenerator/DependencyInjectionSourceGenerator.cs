using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Text;

namespace Vecerdi.Extensions.DependencyInjection.SourceGenerator;

[Generator]
public sealed class DependencyInjectionSourceGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        // No initialization required
    }

    public void Execute(GeneratorExecutionContext context)
    {
        // Look for classes with injection attributes
        var classes = FindMonoBehavioursWithInjection(context.Compilation);
        
        if (classes.Any())
        {
            var generatedSource = GenerateSource(classes.ToArray());
            context.AddSource("DependencyInjectionCache.g.cs", generatedSource);
        }
    }

    private System.Collections.Generic.IEnumerable<ClassInfo> FindMonoBehavioursWithInjection(Compilation compilation)
    {
        var classes = new System.Collections.Generic.List<ClassInfo>();

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var classNode in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var classSymbol = semanticModel.GetDeclaredSymbol(classNode) as INamedTypeSymbol;
                if (classSymbol == null) continue;

                if (!InheritsFromMonoBehaviour(classSymbol)) continue;

                var properties = GetInjectableProperties(classSymbol);
                if (properties.Any())
                {
                    classes.Add(new ClassInfo(
                        classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        classSymbol.Name,
                        classSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty,
                        properties.ToArray()));
                }
            }
        }

        return classes;
    }

    private static bool InheritsFromMonoBehaviour(INamedTypeSymbol classSymbol)
    {
        var currentType = classSymbol;
        while (currentType != null)
        {
            if (currentType.Name == "MonoBehaviour" && 
                currentType.ContainingNamespace?.ToDisplayString() == "UnityEngine")
                return true;

            currentType = currentType.BaseType;
        }
        return false;
    }

    private static System.Collections.Generic.IEnumerable<PropertyInfo> GetInjectableProperties(INamedTypeSymbol classSymbol)
    {
        var properties = new System.Collections.Generic.List<PropertyInfo>();
        
        // Walk up the inheritance chain to collect all injectable properties
        var currentType = classSymbol;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in currentType.GetMembers().OfType<IPropertySymbol>())
            {
                if (member.IsStatic || member.IsIndexer)
                    continue;

                var injectAttribute = member.GetAttributes()
                    .FirstOrDefault(attr => attr.AttributeClass?.Name == "InjectAttribute");
                
                var keyedAttribute = member.GetAttributes()
                    .FirstOrDefault(attr => attr.AttributeClass?.Name == "InjectFromKeyedServicesAttribute");

                if (injectAttribute == null && keyedAttribute == null)
                    continue;

                var isRequired = GetIsRequired(member, injectAttribute, keyedAttribute);
                var serviceKey = GetServiceKey(keyedAttribute);

                properties.Add(new PropertyInfo(
                    member.Name,
                    member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    serviceKey,
                    isRequired));
            }

            currentType = currentType.BaseType;
        }

        return properties;
    }

    private static bool GetIsRequired(IPropertySymbol property, AttributeData? injectAttribute, AttributeData? keyedAttribute)
    {
        // Check RequiredMemberAttribute first
        if (property.GetAttributes().Any(attr => attr.AttributeClass?.Name == "RequiredMemberAttribute"))
            return true;

        // Check IsRequired parameter on injection attributes
        if (injectAttribute?.ConstructorArguments.FirstOrDefault().Value is bool injectRequired)
            return injectRequired;

        if (keyedAttribute?.ConstructorArguments.Skip(1).FirstOrDefault().Value is bool keyedRequired)
            return keyedRequired;

        // Default to true if not specified
        return true;
    }

    private static string? GetServiceKey(AttributeData? keyedAttribute)
    {
        if (keyedAttribute?.ConstructorArguments.FirstOrDefault().Value is { } serviceKey)
        {
            return serviceKey?.ToString();
        }
        return null;
    }

    private static string GenerateSource(ClassInfo[] classes)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine();
        sb.AppendLine("namespace Vecerdi.Extensions.DependencyInjection.Infrastructure");
        sb.AppendLine("{");
        sb.AppendLine("    internal static partial class DependencyInjectionCache");
        sb.AppendLine("    {");
        sb.AppendLine("        static DependencyInjectionCache()");
        sb.AppendLine("        {");
        sb.AppendLine("            InitializeGeneratedInjectionMethods();");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        private static void InitializeGeneratedInjectionMethods()");
        sb.AppendLine("        {");
        
        foreach (var classInfo in classes)
        {
            var methodName = $"Inject{SanitizeIdentifier(classInfo.Name)}";
            sb.AppendLine($"            RegisterInjectionMethod(typeof({classInfo.FullName}), {methodName});");
        }
        
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // Generate injection methods for each class
        foreach (var classInfo in classes)
        {
            GenerateInjectionMethod(sb, classInfo);
        }
        
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GenerateInjectionMethod(StringBuilder sb, ClassInfo classInfo)
    {
        var methodName = $"Inject{SanitizeIdentifier(classInfo.Name)}";
        
        sb.AppendLine($"        private static void {methodName}(object instance, IServiceProvider serviceProvider)");
        sb.AppendLine("        {");
        sb.AppendLine($"            var typedInstance = ({classInfo.FullName})instance;");
        sb.AppendLine();

        foreach (var property in classInfo.Properties)
        {
            if (property.ServiceKey != null)
            {
                // Keyed service injection - use non-generic methods to avoid nullable type issues
                if (property.IsRequired)
                {
                    sb.AppendLine($"            typedInstance.{property.Name} = ({property.Type})((IKeyedServiceProvider)serviceProvider).GetRequiredKeyedService(typeof({GetNonNullableTypeName(property.Type)}), \"{property.ServiceKey}\");");
                }
                else
                {
                    sb.AppendLine($"            typedInstance.{property.Name} = ({property.Type})((IKeyedServiceProvider)serviceProvider).GetKeyedService(typeof({GetNonNullableTypeName(property.Type)}), \"{property.ServiceKey}\");");
                }
            }
            else
            {
                // Regular service injection - use non-generic methods to avoid nullable type issues
                if (property.IsRequired)
                {
                    sb.AppendLine($"            typedInstance.{property.Name} = ({property.Type})serviceProvider.GetRequiredService(typeof({GetNonNullableTypeName(property.Type)}));");
                }
                else
                {
                    sb.AppendLine($"            typedInstance.{property.Name} = ({property.Type})serviceProvider.GetService(typeof({GetNonNullableTypeName(property.Type)}));");
                }
            }
        }
        
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static string GetNonNullableTypeName(string typeName)
    {
        // Remove nullable annotation for value types (e.g., "double?" -> "double")
        // For reference types with nullable annotation (e.g., "string?"), remove the ? but keep the type name
        return typeName.EndsWith("?") ? typeName.TrimEnd('?') : typeName;
    }

    private static string SanitizeIdentifier(string name)
    {
        // Remove any characters that aren't valid in C# identifiers
        var sb = new StringBuilder();
        foreach (var ch in name)
        {
            if (char.IsLetterOrDigit(ch) || ch == '_')
                sb.Append(ch);
            else
                sb.Append('_');
        }
        return sb.ToString();
    }

    private sealed class ClassInfo
    {
        public string FullName { get; }
        public string Name { get; }
        public string Namespace { get; }
        public PropertyInfo[] Properties { get; }

        public ClassInfo(string fullName, string name, string namespaceName, PropertyInfo[] properties)
        {
            FullName = fullName;
            Name = name;
            Namespace = namespaceName;
            Properties = properties;
        }
    }

    private sealed class PropertyInfo
    {
        public string Name { get; }
        public string Type { get; }
        public string? ServiceKey { get; }
        public bool IsRequired { get; }

        public PropertyInfo(string name, string type, string? serviceKey, bool isRequired)
        {
            Name = name;
            Type = type;
            ServiceKey = serviceKey;
            IsRequired = isRequired;
        }
    }
}