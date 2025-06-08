using JetBrains.Annotations;

namespace MediaVault.DependencyInjection.Attributes;

[AttributeUsage(AttributeTargets.Property)]
[MeansImplicitUse(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.Itself)]
public sealed class InjectFromKeyedServicesAttribute(object? serviceKey, bool isRequired = true) : Attribute {
    public object? ServiceKey { get; } = serviceKey;
    public bool IsRequired { get; } = isRequired;
}
