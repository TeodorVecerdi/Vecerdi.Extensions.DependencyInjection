# Dependency Injection Source Generator

This directory contains a Roslyn source generator that replaces runtime reflection with compile-time code generation for discovering MonoBehaviour properties that require dependency injection.

## Overview

The source generator analyzes MonoBehaviour classes at compile time to find properties marked with:
- `[Inject]` - Standard dependency injection
- `[InjectFromKeyedServices]` - Keyed service injection

Instead of using reflection at runtime, the generator pre-computes this information and generates efficient lookup code.

## How It Works

1. **Analysis Phase**: The source generator scans all classes in the compilation that:
   - Inherit from `MonoBehaviour` (directly or indirectly)
   - Have properties decorated with injection attributes

2. **Generation Phase**: For each qualifying class, the generator creates registration code that:
   - Populates a static cache with property information
   - Handles inheritance by walking up the type hierarchy
   - Preserves all attribute parameters (service keys, required flags)

3. **Runtime Phase**: The modified `DependencyInjectionCache` class:
   - First checks the generated cache for pre-computed results
   - Falls back to reflection for types not covered by the generator
   - Maintains full backward compatibility

## Generated Code Example

For a MonoBehaviour like:

```csharp
public class ExampleService : MonoBehaviour
{
    [Inject]
    public ILogger Logger { get; set; }
    
    [InjectFromKeyedServices("database")]
    public IDbContext Database { get; set; }
}
```

The generator produces:

```csharp
// In DependencyInjectionCache.g.cs
internal static partial class DependencyInjectionCache
{
    static DependencyInjectionCache()
    {
        InitializeGeneratedCache();
    }

    private static void InitializeGeneratedCache()
    {
        RegisterGeneratedProperties(typeof(ExampleService), new List<(PropertyInfo, object?, bool)>
        {
            (typeof(ExampleService).GetProperty("Logger")!, null, true),
            (typeof(ExampleService).GetProperty("Database")!, "database", true),
        });
    }
}
```

## Benefits

1. **Performance**: Eliminates reflection overhead at runtime
2. **Compile-time Safety**: Catches missing properties at build time
3. **AOT Compatibility**: Generated code works with ahead-of-time compilation
4. **Backward Compatibility**: Existing code continues to work without changes
5. **Inheritance Support**: Properly handles inheritance hierarchies

## Usage

### For Library Authors

1. Reference the source generator project as an analyzer:
   ```xml
   <ItemGroup>
     <ProjectReference Include="path/to/SourceGenerator.csproj" 
                       OutputItemType="Analyzer" 
                       ReferenceOutputAssembly="false" />
   </ItemGroup>
   ```

2. The generator automatically runs during compilation and generates the cache population code.

### For Unity Projects

Since Unity doesn't directly support .NET source generators, you can:

1. **Pre-build approach**: Run the generator in a separate build step and include the generated files
2. **Manual generation**: Use the generator logic to create static cache initialization code
3. **Future Unity versions**: Unity may add source generator support in future releases

## Architecture

The source generator follows modern Roslyn practices:

- **Incremental Generation**: Uses `ISourceGenerator` interface for efficiency
- **Syntax Analysis**: Filters candidates early to avoid expensive semantic analysis
- **Inheritance Handling**: Walks type hierarchies to collect all injectable properties
- **Error Handling**: Gracefully handles compilation errors and edge cases

## Compatibility

- **Unity**: Compatible with Unity MonoBehaviour injection patterns
- **Reflection Fallback**: Seamlessly falls back to reflection when needed
- **Partial Classes**: Uses partial class pattern for clean separation
- **Modern C#**: Supports latest C# language features and nullable reference types

## Future Enhancements

Potential improvements for the source generator:

1. **Diagnostics**: Add analyzer rules for common injection mistakes
2. **Performance Optimizations**: Further reduce generated code size
3. **Editor Integration**: Provide Visual Studio/Rider tooling support
4. **Unity Editor**: Custom Unity editor tools for source generator management