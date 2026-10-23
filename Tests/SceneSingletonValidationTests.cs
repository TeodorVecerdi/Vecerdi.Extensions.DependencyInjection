using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Vecerdi.Extensions.DependencyInjection.Tests;

// Runs the first-scene check against a session built without play mode. Nothing here registers logging, so a report
// goes to the console through Debug.LogError.
[TestFixture]
public sealed class SceneSingletonValidationTests {
    private readonly List<GameObject> m_Objects = [];
    private ServiceManager? m_Manager;

    [TearDown]
    public void TearDown() {
        m_Manager?.TearDownServiceManager();
        m_Manager = null;

        foreach (var gameObject in m_Objects) {
            if (gameObject != null) Object.DestroyImmediate(gameObject);
        }

        m_Objects.Clear();
    }

    [Test]
    public void MissingThrowSingleton_LogsOneErrorNamingIt() {
        // Arrange
        var manager = Initialize(services => services.AddMonoBehaviourSingleton<FirstThrowProbe>());
        LogAssert.Expect(LogType.Error, new Regex($@"^\[ServiceManager\] MonoBehaviour singletons registered with MissingSingletonInstanceBehavior\.Throw are missing from the loaded scene '.*': {nameof(FirstThrowProbe)}\. Each throws when resolved:"));
        // Act
        manager.ValidateMonoBehaviourSingletons();
        // Assert
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void SeveralMissingThrowSingletons_AreListedInOneError() {
        // Arrange
        var manager = Initialize(services => {
            services.AddMonoBehaviourSingleton<FirstThrowProbe>();
            services.AddMonoBehaviourSingleton<SecondThrowProbe>();
        });
        LogAssert.Expect(LogType.Error, new Regex($@"are missing from the loaded scene '.*': {nameof(FirstThrowProbe)}, {nameof(SecondThrowProbe)}\. Each throws"));
        // Act
        manager.ValidateMonoBehaviourSingletons();
        // Assert
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void PlacedThrowSingleton_LogsNothing() {
        // Arrange
        Place<FirstThrowProbe>(active: true);
        var manager = Initialize(services => services.AddMonoBehaviourSingleton<FirstThrowProbe>());
        // Act
        manager.ValidateMonoBehaviourSingletons();
        // Assert
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void InactivePlacedThrowSingleton_LogsNothing() {
        // Arrange
        Place<FirstThrowProbe>(active: false);
        var manager = Initialize(services => services.AddMonoBehaviourSingleton<FirstThrowProbe>());
        // Act
        manager.ValidateMonoBehaviourSingletons();
        // Assert
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void MissingCreateSingleton_LogsNothing() {
        // Arrange
        var manager = Initialize(services => services.AddMonoBehaviourSingleton<CreateProbe>());
        // Act
        manager.ValidateMonoBehaviourSingletons();
        // Assert
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void ForwardedThrowSingleton_IsReportedOnce() {
        // Arrange
        var manager = Initialize(services => {
            services.AddMonoBehaviourSingleton<FirstThrowProbe>();
            services.AddMonoBehaviourSingleton<IThrowProbe, FirstThrowProbe>();
            services.AddMonoBehaviourSingleton((FirstThrowProbe probe) => probe.name);
        });
        LogAssert.Expect(LogType.Error, new Regex($@"are missing from the loaded scene '.*': {nameof(FirstThrowProbe)}\. Each throws"));
        // Act
        manager.ValidateMonoBehaviourSingletons();
        // Assert
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void ServiceManagerNotInitialized_DoesNothing() {
        // Arrange
        var manager = new ServiceManager();
        // Act
        manager.ValidateMonoBehaviourSingletons();
        // Assert
        LogAssert.NoUnexpectedReceived();
    }

    private ServiceManager Initialize(Action<IServiceCollection> configure) {
        ServiceManager.RegisterServices((services, _) => configure(services));
        m_Manager = new ServiceManager();
        m_Manager.InitializeServiceManager();
        return m_Manager;
    }

    private void Place<T>(bool active) where T : MonoBehaviour {
        var gameObject = new GameObject(typeof(T).Name);
        m_Objects.Add(gameObject);
        gameObject.AddComponent<T>();
        gameObject.SetActive(active);
    }

    internal interface IThrowProbe;

    internal sealed class FirstThrowProbe : MonoSingleton<FirstThrowProbe>, IThrowProbe {
        static FirstThrowProbe() => MissingInstanceBehavior = MissingSingletonInstanceBehavior.Throw;
    }

    internal sealed class SecondThrowProbe : MonoSingleton<SecondThrowProbe> {
        static SecondThrowProbe() => MissingInstanceBehavior = MissingSingletonInstanceBehavior.Throw;
    }

    internal sealed class CreateProbe : MonoSingleton<CreateProbe>;
}
