# Vecerdi.Extensions.DependencyInjection source generator

The Roslyn source generator the DI package ships with. Unity ignores this folder (`~`); the built
`Vecerdi.Extensions.DependencyInjection.SourceGenerator.dll` lives in `../SourceGenerator/`, labeled `RoslynAnalyzer`,
so Unity applies it to the DI assembly and every assembly that references it. How to use it, its rules and its
diagnostics are in the package README's "Source-generated injection" section.

Build and test from `src/`:

```sh
dotnet test src/Vecerdi.Extensions.DependencyInjection.SourceGenerator.slnx
dotnet build src/Vecerdi.Extensions.DependencyInjection.SourceGenerator -c Release   # also publishes the DLL into ../SourceGenerator/
```

`src/Vecerdi.Extensions.DependencyInjection.API` compiles the DI sources the generator targets (the `[Inject*]`
attributes and the type-injector infrastructure; none need Unity), and the tests reference it, so they run against the
real API. Each test declares only `MonoBehaviour` and `BaseMonoBehaviour`, which the generator matches by name.

Previously a separate repository (github.com/TeodorVecerdi/Vecerdi.Extensions.DependencyInjection.SourceGenerator),
which also carried the DI library as a submodule and Unity stubs to compile it; its history was imported with
`git subtree`.
