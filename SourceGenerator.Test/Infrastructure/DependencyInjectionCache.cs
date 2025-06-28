using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Vecerdi.Extensions.DependencyInjection.Infrastructure;

internal static partial class DependencyInjectionCache {
    private static readonly ConcurrentDictionary<Type, Action<object, IServiceProvider>> s_GeneratedInjectionMethods = new();

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
}