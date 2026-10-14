using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using Vecerdi.Extensions.DependencyInjection.Infrastructure;
using Vecerdi.Extensions.DependencyInjection.Internal;
using Vecerdi.Extensions.Lifetime;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Vecerdi.Extensions.DependencyInjection;

[NoAutoStaticsCleanup]
public sealed partial class ServiceManager : IKeyedServiceProvider {
    private static readonly TimeSpan s_ShutdownTimeout = TimeSpan.FromSeconds(5);

    public static ITypeInjectorResolver Resolver { get; set; } = new ReflectionTypeInjectorResolver();

    public static ServiceManager Instance => s_Instance ?? throw new InvalidOperationException("ServiceManager is not initialized yet");

    private static readonly List<Action<IServiceCollection, IConfigurationManager>> s_ServiceRegistrations = [];
    private static readonly List<Action<IConfigurationManager>> s_ConfigurationRegistrations = [];
    private static readonly List<Action<IServiceProvider>> s_PostInitializationActions = [];
    private static readonly List<Action<IServiceProvider>> s_PreTeardownActions = [];
    private static readonly List<Action> s_PostTeardownActions = [];
    private static ServiceManager? s_Instance;

    private readonly object m_HostedServicesGate = new();
    private readonly List<IHostedService> m_HostedServices = [];
    private readonly HostApplicationLifetime m_Lifetime = new(SynchronizationContext.Current);
    private ConfigurationManager? m_Configuration;
    private IServiceProvider? m_ServiceProvider;
    private Task m_Startup = Task.CompletedTask;
    private IDisposable? m_CodeLifetimeRegistration;

    /// <summary>
    /// The play session's lifetime, also registered as <see cref="IHostApplicationLifetime"/>: Started once every hosted
    /// service has finished starting (startup failures are logged and do not hold it back; skipped if the session is
    /// already stopping, so a waiter should also watch Stopping or its own token), Stopping as soon as Unity says the session is ending (every object still alive), Stopped
    /// once hosted services have stopped, just before the provider is disposed.
    /// </summary>
    public IHostApplicationLifetime Lifetime => m_Lifetime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    private static void OnBeforeSplashScreen() {
        if (s_Instance is not null) {
            throw new InvalidOperationException("ServiceManager was already initialized or its instance was not cleared properly.");
        }

        Application.quitting += HandleApplicationQuitting;
        s_Instance = new ServiceManager();
        s_Instance.InitializeServiceManager();
    }

    // Teardown runs from [OnExitingPlayMode]. Probed on 6000.7.0b3, it fires after every OnDisable/OnDestroy on each path
    // that ends a session: leaving play mode, a player quitting, closing the editor while playing, and recompiling
    // during play (objects only get OnDisable there; nothing re-creates the container afterwards). The order of OnDestroy
    // calls on quit is arbitrary, whatever the execution order, so no object can count on being destroyed last; this
    // hook is what keeps services usable from every OnDisable/OnDestroy.
    [OnExitingPlayMode]
    private static void OnExitingPlayMode() {
        var instance = Interlocked.Exchange(ref s_Instance, null);
        instance?.TearDownServiceManager();
    }

    // The session is ending; Stopping goes out now, while every object is still alive, and teardown follows later from
    // [OnExitingPlayMode]. Fires for a player quit and for leaving play mode (not vetoable in the editor). Closing the
    // editor while playing skips it; the code lifetime's stop (EditorApplication.quitting) covers that path.
    private static void HandleApplicationQuitting() => s_Instance?.m_Lifetime.NotifyStopping();

    internal void InitializeServiceManager() {
        var configuration = new ConfigurationManager();
        m_Configuration = configuration;
        s_ConfigurationRegistrations.ForEach(action => action(configuration));
        s_ConfigurationRegistrations.Clear();

        var services = new ServiceCollection();
        s_ServiceRegistrations.ForEach(action => action(services, configuration));
        s_ServiceRegistrations.Clear();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostApplicationLifetime>(m_Lifetime);

        m_ServiceProvider = services.BuildServiceProvider();
        m_CodeLifetimeRegistration = CodeLifetime.OnStopping(nameof(ServiceManager), _ => m_Lifetime.NotifyStopping());

        s_PostInitializationActions.ForEach(action => action(m_ServiceProvider));
        s_PostInitializationActions.Clear();

        // Hosted services are resolved and started on the thread pool, during the splash screen: their
        // constructors and the synchronous head of each StartAsync must not touch MonoBehaviour singletons
        // or main-thread-only Unity APIs. Hop to the main thread explicitly where one is needed.
        m_Startup = Task.Run(StartHostedServicesAsync);
    }

    internal void TearDownServiceManager() {
        Application.quitting -= HandleApplicationQuitting;
        m_CodeLifetimeRegistration?.Dispose();

        // Usually already out (Application.quitting or the code lifetime); first thing otherwise, so the pre-teardown
        // actions, a startup still in flight and the hosted services all see it.
        m_Lifetime.NotifyStopping();

        // Registered while the provider was being built, run here with it still fully alive.
        RunTeardownActions(s_PreTeardownActions, action => action(m_ServiceProvider!));

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

        m_Lifetime.NotifyStopped();

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

        // Registered as an instance, so the container does not dispose it; its providers' file watchers go with it.
        m_Configuration?.Dispose();
        m_Configuration = null;

        // The container is gone: this is where anything that pointed at it reverts to its standalone state.
        RunTeardownActions(s_PostTeardownActions, action => action());
    }

    // Reverse registration order, each action isolated: a failing action must not stop the rest, and
    // nothing here may stop the provider from being disposed. The list is cleared so a re-registration
    // in the next session (play mode without domain reload) does not double up.
    private static void RunTeardownActions<T>(List<T> actions, Action<T> invoke) {
        for (var i = actions.Count - 1; i >= 0; i--) {
            try {
                invoke(actions[i]);
            } catch (Exception ex) {
                Debug.LogException(ex);
            }
        }

        actions.Clear();
    }

    /// <summary>
    /// Registers services with the collection the provider is built from. Runs once, during startup, after
    /// every <see cref="RegisterConfiguration"/> action, so the configuration passed in is complete. Register
    /// from a <c>[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]</c> method: the provider is built at
    /// <c>BeforeSplashScreen</c>, and registrations after that point are ignored until the next session.
    /// </summary>
    public static void RegisterServices(Action<IServiceCollection, IConfigurationManager> configuration) {
        s_ServiceRegistrations.Add(configuration);
    }

    /// <summary>
    /// Adds sources and values to the configuration before any service registration sees it. Runs once,
    /// during startup, before every <see cref="RegisterServices"/> action; the resulting configuration is
    /// also registered as <c>IConfiguration</c>. Same registration timing as <see cref="RegisterServices"/>.
    /// </summary>
    public static void RegisterConfiguration(Action<IConfigurationManager> configuration) {
        s_ConfigurationRegistrations.Add(configuration);
    }

    /// <summary>
    /// Runs right after the provider has been built, on the main thread, before hosted services start. Use it
    /// to hand container services to static consumers or to construct singletons eagerly. Actions run in
    /// registration order; the counterpart is <see cref="RegisterPreTeardownAction"/>.
    /// </summary>
    public static void RegisterPostInitializationAction(Action<IServiceProvider> action) {
        s_PostInitializationActions.Add(action);
    }

    /// <summary>
    /// Runs at the start of teardown, before hosted services stop and while the provider is fully usable.
    /// Mirrors <see cref="RegisterPostInitializationAction"/>: use it to release what was set up there and
    /// still needs the container to do so. Actions run in reverse registration order.
    /// </summary>
    public static void RegisterPreTeardownAction(Action<IServiceProvider> action) {
        s_PreTeardownActions.Add(action);
    }

    /// <summary>
    /// Runs at the end of teardown, after the provider has been disposed. Use it to revert globals that
    /// were pointed at the container back to their standalone state. Actions run in reverse registration order.
    /// </summary>
    public static void RegisterPostTeardownAction(Action action) {
        s_PostTeardownActions.Add(action);
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

        // The task is discarded by the caller: every exception is caught and reported here, or it would go unobserved.
        ILogger? logger = null;
        try {
            logger = m_ServiceProvider.GetService<ILogger<ServiceManager>>();
            var hostedServices = m_ServiceProvider.GetServices<IHostedService>().ToArray();
            lock (m_HostedServicesGate) {
                m_HostedServices.AddRange(hostedServices);
            }

            // Hosted services start concurrently; services that depend on one another should model the
            // dependency explicitly rather than relying on registration order. StartAsync receives
            // ApplicationStopping, which existing services link as their own stopping token; keep it that way.
            await Task.WhenAll(hostedServices.Select(async hostedService => {
                try {
                    await hostedService.StartAsync(m_Lifetime.ApplicationStopping);
                } catch (Exception ex) {
                    LogError(logger, ex, "Error starting hosted service {Name}", hostedService.GetType().Name);
                }
            }));
        } catch (Exception ex) {
            LogError(logger, ex, "Error starting hosted services");
        } finally {
            m_Lifetime.NotifyStarted(); // skipped if the session is already stopping
        }
    }

    private async Task StopHostedServicesAsync() {
        if (m_ServiceProvider is null)
            throw new InvalidOperationException("ServiceManager is not initialized yet");

        // The caller abandons the wait at the same deadline; this tells the services so, as the Generic Host does.
        using var deadline = new CancellationTokenSource(s_ShutdownTimeout);
        var logger = m_ServiceProvider.GetService<ILogger<ServiceManager>>();

        // A startup still in flight saw ApplicationStopping (it is StartAsync's token) and is winding down. Wait for it,
        // so every service that started gets stopped and none is stopped while its StartAsync still runs. Off the main
        // thread from here on: the main thread is blocked waiting for this method.
        await m_Startup.ConfigureAwait(false);

        IHostedService[] hostedServices;
        lock (m_HostedServicesGate) {
            hostedServices = m_HostedServices.ToArray();
            m_HostedServices.Clear();
        }

        // Stop services in reverse order
        for (var i = hostedServices.Length - 1; i >= 0; i--) {
            try {
                await hostedServices[i].StopAsync(deadline.Token).ConfigureAwait(false);
            } catch (Exception ex) {
                LogError(logger, ex, "Error stopping hosted service {Name}", hostedServices[i].GetType().Name);
            }
        }
    }

    // Logging is optional. Without it registered, a failure goes straight to the console; its trace names the service.
    private static void LogError(ILogger? logger, Exception exception, string message, params object?[] args) {
        if (logger is not null) {
            logger.LogError(exception, message, args);
        } else {
            Debug.LogException(exception);
        }
    }
}
