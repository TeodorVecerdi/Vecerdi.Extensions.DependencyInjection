# Vecerdi.Extensions.DependencyInjection analyzers

The Roslyn generator, analyzer and code fix the DI package ships with. Unity ignores this folder (`~`); the built
`Vecerdi.Extensions.DependencyInjection.Analyzers.dll` lives in `../Analyzers/`, labeled `RoslynAnalyzer`, so Unity
applies it to the DI assembly and every assembly that references it.

- **Injector generator:** fills in each `TypeInjectorResolverContext`, so `[Inject]` properties are set without
  reflection. Rules and diagnostics (VDI0001–VDI0007) are in the package's "Source-generated injection" article.
- **VDI0008 analyzer and code fix:** a `Singleton` or `Scoped` factory registration that resolves a
  `MonoSingleton<T>`, rewritten onto `AddMonoBehaviourSingleton`. See the "MonoBehaviour services" article.

The project groups its files by kind, as UnityCore's analyzers do: `Core/` (descriptors), `Generators/`, `Analyzers/`
and `FixProviders/`. The folders are not namespace segments; everything is in
`Vecerdi.Extensions.DependencyInjection.Analyzers`.

Build and test from `src/`:

```sh
dotnet test src/Vecerdi.Extensions.DependencyInjection.Analyzers.slnx
dotnet build src/Vecerdi.Extensions.DependencyInjection.Analyzers -c Release   # also publishes the DLL into ../Analyzers/
```

`src/Vecerdi.Extensions.DependencyInjection.API` compiles the DI sources the generator targets (the `[Inject*]`
attributes and the type-injector infrastructure; none need Unity), and the tests reference it, so they run against the
real API. The tests declare the Unity-dependent types they need (`MonoBehaviour`, `BaseMonoBehaviour`,
`MonoSingleton<T>`), which the generator and the analyzer match by name.

Named `Vecerdi.Extensions.DependencyInjection.SourceGenerator` (folders `SourceGenerator~/` and `SourceGenerator/`)
until 2.0.0, when it gained the analyzer.
