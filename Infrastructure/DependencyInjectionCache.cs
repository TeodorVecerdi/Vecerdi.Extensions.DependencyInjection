using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using MediaVault.DependencyInjection.Attributes;

namespace MediaVault.DependencyInjection.Infrastructure;

internal static class DependencyInjectionCache {
    private static readonly ConcurrentDictionary<Type, CacheInfo> s_Cache = new();

    public static IReadOnlyList<(PropertyInfo PropertyInfo, object? ServiceKey, bool IsRequired)> GetInjectableProperties(Type type) {
        return s_Cache.GetOrAdd(type, t => new CacheInfo(t)).InjectableProperties;
    }

    private static bool IsRequired(PropertyInfo property) {
        return property.GetCustomAttribute<RequiredMemberAttribute>() != null;
    }

    private sealed class CacheInfo(Type type) {
        public List<(PropertyInfo PropertyInfo, object? ServiceKey, bool IsRequired)> InjectableProperties { get; }
            = [
                ..type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Select(p => (Property: p, Attribute: p.GetCustomAttribute<InjectAttribute>(), KeyedAttribute: p.GetCustomAttribute<InjectFromKeyedServicesAttribute>()))
                    .Where(t => t.Attribute != null || t.KeyedAttribute != null)
                    .Select(t => (t.Property, t.KeyedAttribute?.ServiceKey, t.Attribute?.IsRequired ?? t.KeyedAttribute!.IsRequired || IsRequired(t.Property))),
            ];
    }
}
