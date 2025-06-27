// Mock Unity classes for testing
namespace UnityEngine
{
    public class MonoBehaviour { }
}

namespace Vecerdi.Extensions.DependencyInjection
{
    [System.AttributeUsage(System.AttributeTargets.Property)]
    public class InjectAttribute : System.Attribute
    {
        public bool IsRequired { get; }
        public InjectAttribute(bool isRequired = true) => IsRequired = isRequired;
    }

    [System.AttributeUsage(System.AttributeTargets.Property)]
    public class InjectFromKeyedServicesAttribute : System.Attribute
    {
        public object? ServiceKey { get; }
        public bool IsRequired { get; }
        public InjectFromKeyedServicesAttribute(object? serviceKey, bool isRequired = true)
        {
            ServiceKey = serviceKey;
            IsRequired = isRequired;
        }
    }
}

namespace SourceGenerator.Test
{
    using UnityEngine;
    using Vecerdi.Extensions.DependencyInjection;

    public class TestMonoBehaviour : MonoBehaviour
    {
        [Inject]
        public string? TestService { get; set; }

        [InjectFromKeyedServices("test-key")]
        public int KeyedService { get; set; }

        [Inject(false)]
        public object? OptionalService { get; set; }
    }

    public class InheritedTestBehaviour : TestMonoBehaviour
    {
        [Inject]
        public double? AdditionalService { get; set; }
    }
}