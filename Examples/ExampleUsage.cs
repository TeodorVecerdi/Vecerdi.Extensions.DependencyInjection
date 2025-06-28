using System;
using UnityEngine;
using Vecerdi.Extensions.DependencyInjection;

namespace Vecerdi.Extensions.DependencyInjection.Examples;

// Example MonoBehaviour that uses dependency injection
public class ExampleMonoBehaviour : MonoBehaviour
{
    [Inject]
    public string? TestService { get; set; }

    [InjectFromKeyedServices("database")]
    public object? DatabaseService { get; set; }

    [Inject(false)]
    public int? OptionalService { get; set; }
}

// Another example with inheritance
public class BaseService : MonoBehaviour
{
    [Inject]
    public string? BaseProperty { get; set; }
}

public class DerivedService : BaseService
{
    [InjectFromKeyedServices("cache", true)]
    public object? CacheService { get; set; }
}

/* 
 * The source generator produces injection methods like:
 * 
 * private static void InjectExampleMonoBehaviour(object instance, IServiceProvider serviceProvider) {
 *     var typedInstance = (Vecerdi.Extensions.DependencyInjection.Examples.ExampleMonoBehaviour)instance;
 * 
 *     typedInstance.TestService = (string?)serviceProvider.GetRequiredService(typeof(string));
 *     typedInstance.DatabaseService = (object?)((IKeyedServiceProvider)serviceProvider).GetRequiredKeyedService(typeof(object), "database");
 *     typedInstance.OptionalService = (int?)serviceProvider.GetService(typeof(int));
 * }
 * 
 * private static void InjectDerivedService(object instance, IServiceProvider serviceProvider) {
 *     var typedInstance = (Vecerdi.Extensions.DependencyInjection.Examples.DerivedService)instance;
 * 
 *     typedInstance.BaseProperty = (string?)serviceProvider.GetRequiredService(typeof(string));
 *     typedInstance.CacheService = (object?)((IKeyedServiceProvider)serviceProvider).GetRequiredKeyedService(typeof(object), "cache");
 * }
 * 
 * This completely eliminates reflection from the injection process - only the initial
 * method lookup uses a dictionary, then the generated code handles property assignment
 * using direct, strongly-typed property access.
 */