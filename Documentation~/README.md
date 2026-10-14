---
title: Vecerdi.Extensions.DependencyInjection Documentation
description: Documentation index for the Vecerdi.Extensions.DependencyInjection Unity package.
order: 0
tags: [index]
---

# Vecerdi.Extensions.DependencyInjection documentation

Vecerdi.Extensions.DependencyInjection brings `Microsoft.Extensions.DependencyInjection` to Unity: you register
services the way any .NET app does, and `MonoBehaviour`s receive them through `[Inject]` properties. The container
builds itself before the first scene loads, hosted services run during the splash screen, and teardown happens after
every object has shut down, so injected services stay usable from `OnDestroy`.

If you are new, start with [Getting started](getting-started.md).

## Articles

| Article | What it covers |
|---|---|
| [Getting started](getting-started.md) | Requirements, installation, registering services, configuration |
| [Injection](injection.md) | `[Inject]` and keyed injection, `BaseMonoBehaviour`, injection callbacks, resolvers |
| [MonoBehaviour services](monobehaviour-services.md) | Registering `MonoBehaviour`s, `MonoSingleton<T>`, persistence |
| [Lifecycle](lifecycle.md) | Boot order, hosted services, `IHostApplicationLifetime`, teardown |
| [Source-generated injection](source-generation.md) | The bundled generator: setup, rules, diagnostics |
| [Code lifetime](code-lifetime.md) | `Vecerdi.Extensions.Lifetime`: shutting down with code reloads and quits |

## Types at a glance

| Type | Documented in |
|---|---|
| `ServiceManager` | [Getting started](getting-started.md), [Lifecycle](lifecycle.md) |
| `InjectAttribute`, `InjectFromKeyedServicesAttribute` | [Injection](injection.md) |
| `BaseMonoBehaviour`, `MonoBehaviourExtensions.InjectServices` | [Injection](injection.md) |
| `IPostInitializationCallbacks`, `IAllowsReinitialization` | [Injection](injection.md) |
| `ITypeInjectorResolver`, `ReflectionTypeInjectorResolver`, `TypeInjectorResolverCombiner` | [Injection](injection.md) |
| `TypeInjectorResolverContext`, `ExcludeFromInjectionGenerationAttribute` | [Source-generated injection](source-generation.md) |
| `MonoSingleton<T>`, `SingletonPersistence`, `MissingSingletonInstanceBehavior` | [MonoBehaviour services](monobehaviour-services.md) |
| `MonoBehaviourServiceCollectionExtensions` | [MonoBehaviour services](monobehaviour-services.md) |
| `CodeLifetime`, `CodeLifetimeEnd` | [Code lifetime](code-lifetime.md) |
