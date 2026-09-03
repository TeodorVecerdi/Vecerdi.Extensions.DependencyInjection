using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Vecerdi.Extensions.DependencyInjection.Infrastructure;
using Vecerdi.Extensions.DependencyInjection.Internal;

namespace Vecerdi.Extensions.DependencyInjection;

public sealed partial class ServiceManager : IKeyedServiceProvider {
    private static readonly TimeSpan s_ShutdownTimeout = TimeSpan.FromSeconds(5);

    public static ITypeInjectorResolver Resolver { get; set; } = new ReflectionTypeInjectorResolver();

    public static ServiceManager Instance => s_Instance ?? throw new InvalidOperationException("ServiceManager is not initialized yet");

    private static readonly List<Action<IServiceCollection, IConfigurationManager>> s_ServiceRegistrations = [];
    private static readonly List<Action<IConfigurationManager>> s_ConfigurationRegistrations = [];
    private static readonly List<Action<IServiceProvider>> s_PostInitializationActions = [];
    private static ServiceManager? s_Instance;

    private readonly List<IHostedService> m_HostedServices = [];
    private readonly CancellationTokenSource m_ApplicationStoppingCts = new();
    private readonly TaskCompletionSource<bool> m_ReadyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IServiceProvider? m_ServiceProvider;

    /// <summary>
    /// Completes once the service provider has been built and all hosted services have finished starting.
    /// This task never faults: hosted-service startup failures are logged and do not prevent readiness.
    /// </summary>
    public Task WhenReady => m_ReadyTcs.Task;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    private static void OnBeforeSplashScreen() {
        if (s_Instance is not null) {
            throw new InvalidOperationException("ServiceManager was already initialized or its instance was not cleared properly.");
        }

        // Fallback until the host object confirms it is alive (see SpawnHost): if the first scene never loads,
        // Application.quitting is the only teardown we get.
        Application.quitting += HandleApplicationQuitting;
        BehaviourServices.ResetCachesForEnterPlayMode();
        s_Instance = new ServiceManager();
        s_Instance.InitializeServiceManager();
    }

    // Teardown is owned by a hidden host object and runs from its OnDestroy. Application.quitting fires BEFORE
    // scene objects are destroyed, which would leave every other OnDestroy/OnDisable with a disposed provider;
    // OnDestroy honours script execution order (ascending, verified 2026-09-03), so a host at int.MaxValue is
    // destroyed after every other script and services stay usable throughout teardown. Spawned at
    // BeforeSceneLoad, the earliest point where creating GameObjects is allowed.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void SpawnHost() {
        var gameObject = new GameObject("[ServiceManager]") { hideFlags = HideFlags.HideInHierarchy };
        UnityEngine.Object.DontDestroyOnLoad(gameObject);
        gameObject.AddComponent<Host>();
    }

    [DefaultExecutionOrder(int.MaxValue)]
    private sealed class Host : MonoBehaviour {
        private void Awake() {
            Application.quitting -= HandleApplicationQuitting;
        }

        private void OnDestroy() {
            TearDown();
        }
    }

    [OnExitingPlayMode]
    private static void OnExitingPlayMode() {
        Application.quitting -= HandleApplicationQuitting;

        // Fires after scene teardown, so this only runs when the host never existed.
        TearDown();
    }

    private static void HandleApplicationQuitting() {
        TearDown();
    }

    private static void TearDown() {
        var instance = Interlocked.Exchange(ref s_Instance, null);
        instance?.TearDownServiceManager();
    }

    private void InitializeServiceManager() {
        var configuration = new ConfigurationManager();
        s_ConfigurationRegistrations.ForEach(action => action(configuration));
        s_ConfigurationRegistrations.Clear();

        var services = new ServiceCollection();
        s_ServiceRegistrations.ForEach(action => action(services, configuration));
        s_ServiceRegistrations.Clear();

        services.AddSingleton<IConfiguration>(configuration);

        m_ServiceProvider = services.BuildServiceProvider();

        s_PostInitializationActions.ForEach(action => action(m_ServiceProvider));
        s_PostInitializationActions.Clear();

        // Hosted services are resolved and started on the thread pool, during the splash screen: their
        // constructors and the synchronous head of each StartAsync must not touch MonoBehaviour singletons
        // or main-thread-only Unity APIs. Hop to the main thread explicitly where one is needed.
        Task.Run(StartHostedServicesAsync);
    }

    private void TearDownServiceManager() {
        // Hosted services must finish stopping *before* the provider their dependencies live in is
        // disposed. Blocking the main thread with a bounded wait is intentional here: this runs during
        // teardown, and racing the disposal is worse than a short stall on exit.
        try {
            if (!StopHostedServicesAsync().Wait(s_ShutdownTimeout)) {
                Debug.LogWarning($"[{nameof(ServiceManager)}] Hosted services did not stop within {s_ShutdownTimeout.TotalSeconds:0}s; continuing with service provider disposal.");
            }
        } catch (AggregateException ex) {
            Debug.LogException(ex.GetBaseException());
        }

        // ReSharper disable SuspiciousTypeConversion.Global
        if (m_ServiceProvider is IAsyncDisposable asyncDisposable) {
            if (!asyncDisposable.DisposeAsync().AsTask().Wait(s_ShutdownTimeout)) {
                Debug.LogWarning($"[{nameof(ServiceManager)}] Service provider did not dispose within {s_ShutdownTimeout.TotalSeconds:0}s.");
            }
        } else if (m_ServiceProvider is IDisposable disposable) {
            disposable.Dispose();
        }
        // ReSharper restore SuspiciousTypeConversion.Global

        m_ServiceProvider = null;
        m_ApplicationStoppingCts.Dispose();
    }

    public static void RegisterServices(Action<IServiceCollection, IConfigurationManager> configuration) {
        s_ServiceRegistrations.Add(configuration);
    }

    public static void RegisterConfiguration(Action<IConfigurationManager> configuration) {
        s_ConfigurationRegistrations.Add(configuration);
    }

    public static void RegisterPostInitializationAction(Action<IServiceProvider> action) {
        s_PostInitializationActions.Add(action);
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

        if (m_ServiceProvider is IKeyedServiceProvider keyedServiceProvider) {
            return keyedServiceProvider.GetKeyedService(serviceType, serviceKey);
        }

        throw new InvalidOperationException("The underlying service provider does not support keyed services.");
    }

    public object GetRequiredKeyedService(Type serviceType, object? serviceKey) {
        if (m_ServiceProvider is null) {
            throw new InvalidOperationException("ServiceManager is not initialized yet");
        }

        return m_ServiceProvider.GetRequiredKeyedService(serviceType, serviceKey);
    }

    private async Task StartHostedServicesAsync() {
        if (m_ServiceProvider is null)
            throw new InvalidOperationException("ServiceManager is not initialized yet");

        ILogger<ServiceManager> logger;
        try {
            logger = m_ServiceProvider.GetRequiredService<ILogger<ServiceManager>>();
        } catch (Exception ex) {
            // The task is discarded by the caller: an exception here would be unobserved and WhenReady would never complete.
            Debug.LogException(ex);
            m_ReadyTcs.TrySetResult(true);
            return;
        }

        try {
            var hostedServices = m_ServiceProvider.GetServices<IHostedService>();
            m_HostedServices.AddRange(hostedServices);

            // Hosted services start concurrently; services that depend on one another should model the
            // dependency explicitly rather than relying on registration order.
            await Task.WhenAll(m_HostedServices.Select(async hostedService => {
                try {
                    await hostedService.StartAsync(m_ApplicationStoppingCts.Token);
                } catch (Exception ex) {
                    logger.LogError(ex, "Error starting hosted service {Name}", hostedService.GetType().Name);
                }
            }));
        } catch (Exception ex) {
            logger.LogError(ex, "Error starting hosted services");
        } finally {
            m_ReadyTcs.TrySetResult(true);
        }
    }

    private async Task StopHostedServicesAsync() {
        if (m_ServiceProvider is null)
            throw new InvalidOperationException("ServiceManager is not initialized yet");
        if (m_HostedServices.Count == 0)
            return;

        var logger = m_ServiceProvider.GetRequiredService<ILogger<ServiceManager>>();
        try {
            m_ApplicationStoppingCts.Cancel();

            // Stop services in reverse order
            for (var i = m_HostedServices.Count - 1; i >= 0; i--) {
                try {
                    await m_HostedServices[i].StopAsync(CancellationToken.None);
                } catch (Exception ex) {
                    logger.LogError(ex, "Error stopping hosted service {Name}", m_HostedServices[i].GetType().Name);
                }
            }

            m_HostedServices.Clear();
        } catch (Exception ex) {
            logger.LogError(ex, "Error stopping hosted services");
        }
    }
}
