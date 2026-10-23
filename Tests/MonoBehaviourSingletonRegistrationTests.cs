using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vecerdi.Extensions.DependencyInjection.Tests;

// Every test places its components before resolving them: a component the container has to create is made
// DontDestroyOnLoad, which throws outside play mode.
[TestFixture]
public sealed class MonoBehaviourSingletonRegistrationTests {
    private readonly List<GameObject> m_Objects = [];

    [TearDown]
    public void TearDown() {
        foreach (var gameObject in m_Objects) {
            if (gameObject != null) Object.DestroyImmediate(gameObject);
        }

        m_Objects.Clear();
    }

    [Test]
    public void AddMonoBehaviourSingleton_RegistersTheServiceAsTransient() {
        // Arrange
        var services = new ServiceCollection();
        // Act
        services.AddMonoBehaviourSingleton<SingletonProbe>();
        // Assert
        var descriptor = services.Single(item => item.ServiceType == typeof(SingletonProbe));
        Assert.That(descriptor.Lifetime, Is.EqualTo(ServiceLifetime.Transient));
    }

    [Test]
    public void Resolve_AfterTheInstanceIsReplaced_ReturnsTheReplacement() {
        // Arrange
        var original = Place<SingletonProbe>();
        using var provider = new ServiceCollection().AddMonoBehaviourSingleton<SingletonProbe>().BuildServiceProvider();
        Assert.That(provider.GetRequiredService<SingletonProbe>(), Is.SameAs(original));
        Object.DestroyImmediate(original.gameObject);
        var replacement = Place<SingletonProbe>();
        // Act
        var resolved = provider.GetRequiredService<SingletonProbe>();
        // Assert
        Assert.That(resolved, Is.SameAs(replacement));
    }

    [Test]
    public void Resolve_PlainMonoBehaviourAfterTheInstanceIsReplaced_ReturnsTheReplacement() {
        // Arrange
        var original = Place<PlainProbe>();
        using var provider = new ServiceCollection().AddMonoBehaviourSingleton<PlainProbe>().BuildServiceProvider();
        Assert.That(provider.GetRequiredService<PlainProbe>(), Is.SameAs(original));
        Object.DestroyImmediate(original.gameObject);
        var replacement = Place<PlainProbe>();
        // Act
        var resolved = provider.GetRequiredService<PlainProbe>();
        // Assert
        Assert.That(resolved, Is.SameAs(replacement));
    }

    [Test]
    public void SeveralServicesForOneSingleton_ShareTheInstanceAndTheRegistration() {
        // Arrange
        var placed = Place<SharedProbe>();
        var services = new ServiceCollection();
        services.AddMonoBehaviourSingleton<SharedProbe>();
        services.AddMonoBehaviourSingleton<IProbeService, SharedProbe>();
        services.AddKeyedMonoBehaviourSingleton<IProbeService, SharedProbe>("keyed");
        using var provider = services.BuildServiceProvider();
        // Act
        var registrations = provider.GetServices<IMonoBehaviourSingletonRegistration>().ToArray();
        // Assert
        Assert.That(provider.GetRequiredService<SharedProbe>(), Is.SameAs(placed));
        Assert.That(provider.GetRequiredService<IProbeService>(), Is.SameAs(placed));
        Assert.That(provider.GetRequiredKeyedService<IProbeService>("keyed"), Is.SameAs(placed));
        Assert.That(registrations, Has.Length.EqualTo(1));
        Assert.That(registrations[0], Is.SameAs(provider.GetRequiredService<MonoBehaviourSingletonAccessor<SharedProbe>>()));
    }

    [Test]
    public void Resolve_FromAScope_InjectsFromTheRootProvider() {
        // Arrange
        Place<ScopedConsumerProbe>();
        var services = new ServiceCollection();
        services.AddScoped<ScopedDependency>();
        services.AddMonoBehaviourSingleton<ScopedConsumerProbe>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        // Act
        var probe = scope.ServiceProvider.GetRequiredService<ScopedConsumerProbe>();
        // Assert
        Assert.That(probe.Dependency, Is.Not.SameAs(scope.ServiceProvider.GetRequiredService<ScopedDependency>()));
        Assert.That(probe.Dependency, Is.SameAs(provider.GetRequiredService<ScopedDependency>()));
    }

    [Test]
    public void Resolve_Repeatedly_InjectsEachInstanceOnce() {
        // Arrange
        var original = Place<ReinitializingProbe>();
        using var provider = new ServiceCollection().AddMonoBehaviourSingleton<ReinitializingProbe>().BuildServiceProvider();
        // Act
        provider.GetRequiredService<ReinitializingProbe>();
        provider.GetRequiredService<ReinitializingProbe>();
        Object.DestroyImmediate(original.gameObject);
        var replacement = Place<ReinitializingProbe>();
        provider.GetRequiredService<ReinitializingProbe>();
        provider.GetRequiredService<ReinitializingProbe>();
        // Assert
        Assert.That(original.Injections, Is.EqualTo(1));
        Assert.That(replacement.Injections, Is.EqualTo(1));
    }

    [Test]
    public void AddMonoBehaviourSingleton_WithADisposableComponent_Throws() {
        // Arrange
        var services = new ServiceCollection();
        // Act
        var exception = Assert.Throws<NotSupportedException>(() => services.AddMonoBehaviourSingleton<DisposableProbe>());
        // Assert
        Assert.That(exception!.Message, Does.StartWith($"{nameof(DisposableProbe)} implements IDisposable or IAsyncDisposable"));
        Assert.That(services, Is.Empty);
    }

    [Test]
    public void AddMonoBehaviourSingleton_WithADisposableSelectorServiceType_Throws() {
        // Arrange
        var services = new ServiceCollection();
        // Act
        var exception = Assert.Throws<NotSupportedException>(() =>
            services.AddMonoBehaviourSingleton((FeatureOwnerProbe owner) => (DisposableProbeFeature)owner.DisposableFeature));
        // Assert
        Assert.That(exception!.Message, Does.StartWith($"{nameof(DisposableProbeFeature)} forwarded from {nameof(FeatureOwnerProbe)}"));
    }

    [Test]
    public void Resolve_SelectorReturningADisposable_Throws() {
        // Arrange
        Place<FeatureOwnerProbe>();
        using var provider = new ServiceCollection()
            .AddMonoBehaviourSingleton((FeatureOwnerProbe owner) => owner.DisposableFeature)
            .BuildServiceProvider();
        // Act
        var exception = Assert.Throws<NotSupportedException>(() => provider.GetRequiredService<IProbeFeature>());
        // Assert
        Assert.That(exception!.Message, Does.StartWith($"{nameof(IProbeFeature)} forwarded from {nameof(FeatureOwnerProbe)}"));
    }

    [Test]
    public void Selector_ResolvesAgainstTheCurrentInstance() {
        // Arrange
        var original = Place<FeatureOwnerProbe>();
        using var provider = new ServiceCollection()
            .AddMonoBehaviourSingleton((FeatureOwnerProbe owner) => owner.Feature)
            .BuildServiceProvider();
        Assert.That(provider.GetRequiredService<IProbeFeature>(), Is.SameAs(original.Feature));
        Object.DestroyImmediate(original.gameObject);
        var replacement = Place<FeatureOwnerProbe>();
        // Act
        var resolved = provider.GetRequiredService<IProbeFeature>();
        // Assert
        Assert.That(resolved, Is.SameAs(replacement.Feature));
    }

    [Test]
    public void KeyedSelector_ReceivesTheRequestedKey() {
        // Arrange
        var owner = Place<FeatureOwnerProbe>();
        using var provider = new ServiceCollection()
            .AddKeyedMonoBehaviourSingleton<ProbeChannel, FeatureOwnerProbe>(KeyedService.AnyKey, (probe, key) => probe.GetChannel(key))
            .BuildServiceProvider();
        // Act
        var music = provider.GetRequiredKeyedService<ProbeChannel>("music");
        var effects = provider.GetRequiredKeyedService<ProbeChannel>("effects");
        // Assert
        Assert.That(music.Key, Is.EqualTo("music"));
        Assert.That(effects.Key, Is.EqualTo("effects"));
        Assert.That(music, Is.SameAs(owner.GetChannel("music")));
    }

    [Test]
    public void Registration_ReportsTheConfigurationTheTypeSets() {
        // Arrange
        using var provider = new ServiceCollection().AddMonoBehaviourSingleton<ConfiguredProbe>().BuildServiceProvider();
        // Act
        var registration = provider.GetServices<IMonoBehaviourSingletonRegistration>().Single();
        // Assert
        Assert.That(registration.Type, Is.EqualTo(typeof(ConfiguredProbe)));
        Assert.That(registration.MissingInstanceBehavior, Is.EqualTo(MissingSingletonInstanceBehavior.Throw));
        Assert.That(registration.Persistence, Is.EqualTo(SingletonPersistence.Application));
    }

    [Test]
    public void Registration_ForAPlainMonoBehaviour_ReportsTheDefaults() {
        // Arrange
        using var provider = new ServiceCollection().AddMonoBehaviourSingleton<PlainProbe>().BuildServiceProvider();
        // Act
        var registration = provider.GetServices<IMonoBehaviourSingletonRegistration>().Single();
        // Assert
        Assert.That(registration.Type, Is.EqualTo(typeof(PlainProbe)));
        Assert.That(registration.MissingInstanceBehavior, Is.EqualTo(MissingSingletonInstanceBehavior.Create));
        Assert.That(registration.Persistence, Is.EqualTo(SingletonPersistence.Scene));
    }

    private T Place<T>() where T : MonoBehaviour {
        var gameObject = new GameObject(typeof(T).Name);
        m_Objects.Add(gameObject);
        return gameObject.AddComponent<T>();
    }

    internal interface IProbeService;

    internal interface IProbeFeature;

    internal sealed class ProbeFeature : IProbeFeature;

    internal sealed class DisposableProbeFeature : IProbeFeature, IDisposable {
        public void Dispose() { }
    }

    internal sealed class ProbeChannel(object? key) {
        public object? Key { get; } = key;
    }

    internal sealed class ScopedDependency;

    internal sealed class SingletonProbe : MonoSingleton<SingletonProbe>;

    internal sealed class PlainProbe : MonoBehaviour;

    internal sealed class SharedProbe : MonoSingleton<SharedProbe>, IProbeService;

    internal sealed class ScopedConsumerProbe : MonoSingleton<ScopedConsumerProbe> {
        [Inject] internal ScopedDependency Dependency { get; set; } = null!;
    }

    internal sealed class ReinitializingProbe : MonoSingleton<ReinitializingProbe>, IAllowsReinitialization, IPostInitializationCallbacks {
        public int Injections { get; private set; }

        public void OnServicesInitialized() => Injections++;
    }

    internal sealed class DisposableProbe : MonoSingleton<DisposableProbe>, IDisposable {
        public void Dispose() { }
    }

    internal sealed class FeatureOwnerProbe : MonoSingleton<FeatureOwnerProbe> {
        private readonly Dictionary<object, ProbeChannel> m_Channels = [];

        public IProbeFeature Feature { get; } = new ProbeFeature();
        public IProbeFeature DisposableFeature { get; } = new DisposableProbeFeature();

        public ProbeChannel GetChannel(object? key) {
            if (!m_Channels.TryGetValue(key!, out var channel)) {
                channel = new ProbeChannel(key);
                m_Channels.Add(key!, channel);
            }

            return channel;
        }
    }

    internal sealed class ConfiguredProbe : MonoSingleton<ConfiguredProbe> {
        static ConfiguredProbe() {
            MissingInstanceBehavior = MissingSingletonInstanceBehavior.Throw;
            Persistence = SingletonPersistence.Application;
        }
    }
}
