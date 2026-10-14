---
title: Source-generated injection
description: The bundled source generator - declaring a context, wiring the resolver, eligibility rules, and diagnostics.
order: 5
tags: [injection, source-generation]
sources:
  - SourceGenerator/
  - SourceGenerator~/src/Vecerdi.Extensions.DependencyInjection.SourceGenerator/
  - Infrastructure/TypeInjectorResolverContext.cs
  - Attributes/ExcludeFromInjectionGenerationAttribute.cs
---

# Source-generated injection

By default, injection discovers `[Inject]` properties with reflection. The package also ships a source generator that
writes an injector for each component at compile time: direct property assignments, no reflection. Unity applies it
to every assembly that references this package; it does nothing until you declare a context.

## Declaring a context

Declare one `partial` class deriving from `TypeInjectorResolverContext` in an assembly whose components you want
covered. The generator fills in its `GetTypeInjector`:

```csharp
using Vecerdi.Extensions.DependencyInjection.Infrastructure;

namespace MyGame;

public partial class MyGameTypeInjectorResolverContext : TypeInjectorResolverContext;
```

Then make it the resolver, with reflection behind it for everything a context doesn't cover (other assemblies,
excluded types):

```csharp
using UnityEngine;
using Vecerdi.Extensions.DependencyInjection;
using Vecerdi.Extensions.DependencyInjection.Infrastructure;

internal static class InjectionSetup {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void UseGeneratedInjectors() {
        ServiceManager.Resolver = new TypeInjectorResolverCombiner(
            new MyGameTypeInjectorResolverContext(),
            new ReflectionTypeInjectorResolver());
    }
}
```

## Several assemblies

A context only covers the assembly it's declared in. With injected components in several assemblies, each one gets its
own context, and a single place lists them all:

```
MyGame.Gameplay        ← GameplayTypeInjectorResolverContext
MyGame.UI              ← UITypeInjectorResolverContext
MyGame.Debug (editor)  ← DebugTypeInjectorResolverContext
MyGame.Composition     ← references all three; sets ServiceManager.Resolver
```

That place is usually an assembly of its own. Setting the resolver means naming every context type, and an assembly
can't reference one that already references it, so the setup has to sit above all of them: a small composition
assembly whose asmdef references every assembly with a context (plus this package).

```csharp
using UnityEngine;
using Vecerdi.Extensions.DependencyInjection;
using Vecerdi.Extensions.DependencyInjection.Infrastructure;

namespace MyGame.Composition;

internal static class InjectionSetup {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void UseGeneratedInjectors() {
        ServiceManager.Resolver = new TypeInjectorResolverCombiner(
            new MyGame.Gameplay.GameplayTypeInjectorResolverContext(),
            new MyGame.UI.UITypeInjectorResolverContext(),
#if UNITY_EDITOR
            new MyGame.Debug.DebugTypeInjectorResolverContext(), // not in players
#endif
            new ReflectionTypeInjectorResolver());
    }
}
```

- **Contexts must be `public`** so the composition assembly can construct them.
- **Guard editor-only contexts with `#if UNITY_EDITOR`**, so player builds don't name a type that isn't compiled for
  them.
- **Order doesn't matter between contexts**, since each covers a different assembly; only the reflection resolver has
  to come last.
- **An assembly without a context falls back to reflection**, including third-party assemblies you can't add one
  to. That still works, just without the generated speed-up.

## What gets an injector

- **Types:** non-abstract classes deriving from `MonoBehaviour` that have at least one injectable property, plus every
  `BaseMonoBehaviour` (one without properties shares a no-op injector). Mark a type
  `[ExcludeFromInjectionGeneration]` to leave it to the next resolver.
- **Properties:** exactly one `[Inject]` or `[InjectFromKeyedServices]`, a `public`, `internal` or
  `protected internal` setter, not `init`-only. Inherited properties are included.
- **Property types:** an `IServiceProvider` property receives the provider itself. `IEnumerable<T>`, `T[]`,
  `IReadOnlyList<T>`, `IReadOnlyCollection<T>`, `List<T>`, `IList<T>` and `ICollection<T>` receive every registered
  `T`.
- **Service keys:** `string`, `bool`, `char`, the numeric types, or `null`.

## Diagnostics

| Code | Severity | Meaning |
|---|---|---|
| VDI0001 | Warning | The property is `init`-only and can't be injected |
| VDI0002 | Warning | Generic context classes aren't supported |
| VDI0003 | Info | More than one context in the assembly |
| VDI0004 | Info | The context found no eligible types |
| VDI0005 | Warning | The property's setter isn't accessible to generated code |
| VDI0006 | Error | The property has more than one inject attribute |
| VDI0007 | Warning | A service key on an `IServiceProvider` property is ignored |

A property reported with a warning is skipped, not injected by reflection instead: the type as a whole is covered by
its generated injector.

## Building the generator

The generator's source, tests and solution live in `SourceGenerator~/` (Unity ignores it); `SourceGenerator/` holds
the built DLL. A Release build republishes it:

```sh
dotnet test SourceGenerator~/src/Vecerdi.Extensions.DependencyInjection.SourceGenerator.slnx
dotnet build SourceGenerator~/src/Vecerdi.Extensions.DependencyInjection.SourceGenerator -c Release
```

## See also

- [Injection](injection.md): the attributes and resolvers this builds on.
