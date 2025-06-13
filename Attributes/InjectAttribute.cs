using JetBrains.Annotations;

namespace Vecerdi.Extensions.DependencyInjection;

[AttributeUsage(AttributeTargets.Property)]
[MeansImplicitUse(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.Itself)]
public sealed class InjectAttribute(bool isRequired = true) : Attribute {
    public bool IsRequired { get; } = isRequired;
}
