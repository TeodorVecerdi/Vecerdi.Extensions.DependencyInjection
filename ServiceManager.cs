using MediaVault.DependencyInjection.Singletons;
using MediaVault.Vault;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace MediaVault.DependencyInjection;

[DefaultExecutionOrder(-10000)]
public sealed class ServiceManager : MonoSingleton<ServiceManager>, IKeyedServiceProvider {
    private static readonly List<Action<IServiceCollection, IConfigurationManager>> s_ServiceRegistrations = [];
    private IServiceProvider? m_ServiceProvider;

    protected override void Awake() {
        base.Awake();

        var configuration = new ConfigurationManager();
        configuration.AddVault(["*.json"]);

        var services = new ServiceCollection();
        s_ServiceRegistrations.ForEach(action => action(services, configuration));
        s_ServiceRegistrations.Clear();

        services.AddSingleton<IServiceProvider>(this);
        services.AddSingleton<IKeyedServiceProvider>(this);
        services.AddSingleton<IConfiguration>(configuration);

        m_ServiceProvider = services.BuildServiceProvider();
    }

    protected override void OnDestroy() {
        base.OnDestroy();

        // ReSharper disable SuspiciousTypeConversion.Global
        if (m_ServiceProvider is IAsyncDisposable asyncDisposable) {
            _ = asyncDisposable.DisposeAsync();
        } else if (m_ServiceProvider is IDisposable disposable) {
            disposable.Dispose();
        }
        // ReSharper restore SuspiciousTypeConversion.Global

        m_ServiceProvider = null;
    }

    public static void RegisterServices(Action<IServiceCollection, IConfigurationManager> configuration) {
        s_ServiceRegistrations.Add(configuration);
    }

    object? IServiceProvider.GetService(Type serviceType) {
        if (m_ServiceProvider is null) {
            throw new InvalidOperationException("ServiceManager is not initialized yet");
        }

        return m_ServiceProvider.GetService(serviceType);
    }

    public object? GetKeyedService(Type serviceType, object? serviceKey) {
        if (m_ServiceProvider is null) {
            throw new InvalidOperationException("ServiceManager is not initialized yet");
        }

        return m_ServiceProvider.GetKeyedServices(serviceType, serviceKey).FirstOrDefault();
    }

    public object GetRequiredKeyedService(Type serviceType, object? serviceKey) {
        if (m_ServiceProvider is null) {
            throw new InvalidOperationException("ServiceManager is not initialized yet");
        }

        return m_ServiceProvider.GetKeyedServices(serviceType, serviceKey).FirstOrDefault()
            ?? throw new InvalidOperationException($"Service {serviceType} with key {serviceKey} is not registered.");
    }
}
