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