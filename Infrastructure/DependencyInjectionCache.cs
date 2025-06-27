using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Vecerdi.Extensions.DependencyInjection.Infrastructure;

internal static partial class DependencyInjectionCache {
    private static readonly ConcurrentDictionary<Type, CacheInfo> s_ReflectionCache = new();
    
    // This will be populated by the source generator if available
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<(PropertyInfo PropertyInfo, object? ServiceKey, bool IsRequired)>> s_GeneratedCache = new();

    public static IReadOnlyList<(PropertyInfo PropertyInfo, object? ServiceKey, bool IsRequired)> GetInjectableProperties(Type type) {
        // First, try to get from generated cache (populated by source generator)
        if (s_GeneratedCache.TryGetValue(type, out var generatedProperties)) {
            return generatedProperties;
        }
        
        // Fall back to reflection-based discovery
        return s_ReflectionCache.GetOrAdd(type, t => new CacheInfo(t)).InjectableProperties;
    }

    // This method will be called by the source generator to register pre-computed cache entries
    internal static void RegisterGeneratedProperties(Type type, IReadOnlyList<(PropertyInfo PropertyInfo, object? ServiceKey, bool IsRequired)> properties) {
        s_GeneratedCache[type] = properties;
    }

    // Helper method to check if a type has generated cache entries (useful for debugging/diagnostics)
    public static bool HasGeneratedCache(Type type) {
        return s_GeneratedCache.ContainsKey(type);
    }

    // Get count of types with generated cache entries
    public static int GeneratedCacheCount => s_GeneratedCache.Count;

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
