using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

namespace Vecerdi.Extensions.DependencyInjection.Infrastructure;

internal static partial class DependencyInjectionCache {
    private static readonly ConcurrentDictionary<Type, CacheInfo> s_ReflectionCache = new();
    
    // Delegate for generated injection methods
    private static readonly ConcurrentDictionary<Type, Action<object, IServiceProvider>> s_GeneratedInjectionMethods = new();

    public static void InjectServices(Type type, object instance, IServiceProvider serviceProvider) {
        // First, try to use generated injection method (no reflection)
        if (s_GeneratedInjectionMethods.TryGetValue(type, out var injectionMethod)) {
            injectionMethod(instance, serviceProvider);
            return;
        }
        
        // Fall back to reflection-based injection
        var cacheInfo = s_ReflectionCache.GetOrAdd(type, t => new CacheInfo(t));
        foreach (var (property, serviceKey, isRequired) in cacheInfo.InjectableProperties) {
            var service = serviceKey is not null
                ? (serviceProvider as IKeyedServiceProvider)?.GetKeyedService(property.PropertyType, serviceKey)
                : serviceProvider.GetService(property.PropertyType);
            
            if (service is null) {
                if (isRequired) {
                    throw new InvalidOperationException($"Required service {property.PropertyType} is not registered.");
                }
                continue;
            }

            property.SetValue(instance, service);
        }
    }

    // Legacy method for backward compatibility - returns properties for reflection-based injection
    public static IReadOnlyList<(PropertyInfo PropertyInfo, object? ServiceKey, bool IsRequired)> GetInjectableProperties(Type type) {
        // If we have a generated injection method, we don't need to return properties for reflection
        if (s_GeneratedInjectionMethods.ContainsKey(type)) {
            return Array.Empty<(PropertyInfo, object?, bool)>();
        }
        
        // Fall back to reflection-based discovery
        return s_ReflectionCache.GetOrAdd(type, t => new CacheInfo(t)).InjectableProperties;
    }

    // This method will be called by the source generator to register injection methods
    internal static void RegisterInjectionMethod(Type type, Action<object, IServiceProvider> injectionMethod) {
        s_GeneratedInjectionMethods[type] = injectionMethod;
    }

    // Helper method to check if a type has generated injection method (useful for debugging/diagnostics)
    public static bool HasGeneratedInjectionMethod(Type type) {
        return s_GeneratedInjectionMethods.ContainsKey(type);
    }

    // Get count of types with generated injection methods
    public static int GeneratedInjectionMethodCount => s_GeneratedInjectionMethods.Count;

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
