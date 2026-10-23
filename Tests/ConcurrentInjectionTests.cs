using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Vecerdi.Extensions.DependencyInjection.Infrastructure;
using Vecerdi.Extensions.DependencyInjection.Internal;
using Object = UnityEngine.Object;

namespace Vecerdi.Extensions.DependencyInjection.Tests;

// Injection is not limited to the main thread: these run it from the thread pool. The components are created on the
// main thread, as they have to be.
[TestFixture]
public sealed class ConcurrentInjectionTests {
    private readonly List<GameObject> m_Objects = [];

    [TearDown]
    public void TearDown() {
        foreach (var gameObject in m_Objects) {
            if (gameObject != null) Object.DestroyImmediate(gameObject);
        }

        m_Objects.Clear();
    }

    [Test]
    public void ConcurrentInjection_InjectsEachInstanceOnce() {
        // Arrange: plain MonoBehaviours go through the shared injected-instances tracker, BaseMonoBehaviours through
        // their own flag; both share the injector cache.
        var plain = Enumerable.Range(0, 200).Select(_ => Place<PlainProbe>()).ToArray();
        var based = Enumerable.Range(0, 200).Select(_ => Place<BasedProbe>()).ToArray();
        var resolver = new ReflectionTypeInjectorResolver();
        var provider = new EmptyServiceProvider();
        var instances = plain.Cast<MonoBehaviour>().Concat(based).ToArray();
        // Act
        Parallel.ForEach(instances, new ParallelOptions { MaxDegreeOfParallelism = 8 }, instance => {
            BehaviourServices.InjectIntoMonoBehaviourProperties(provider, resolver, instance);
            BehaviourServices.InjectIntoMonoBehaviourProperties(provider, resolver, instance);
        });
        // Assert
        Assert.That(plain.Select(probe => probe.Injections), Is.All.EqualTo(1));
        Assert.That(based.Select(probe => probe.Injections), Is.All.EqualTo(1));
    }

    [Test]
    public void FailedInjectionOffTheMainThread_NamesTheTypeWithoutItsPath() {
        // Arrange
        var component = Place<RequiresServiceProbe>();
        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => Task.Run(() =>
            BehaviourServices.InjectIntoMonoBehaviourProperties(new EmptyServiceProvider(), new ReflectionTypeInjectorResolver(), component)).GetAwaiter().GetResult());
        // Assert
        Assert.That(exception!.Message, Is.EqualTo($"Could not inject services into {nameof(RequiresServiceProbe)}."));
        Assert.That(exception.InnerException!.Message, Does.Contain(nameof(IMissingService)));
    }

    private T Place<T>() where T : MonoBehaviour {
        var gameObject = new GameObject(typeof(T).Name);
        m_Objects.Add(gameObject);
        return gameObject.AddComponent<T>();
    }

    internal interface IMissingService;

    internal sealed class PlainProbe : MonoBehaviour, IPostInitializationCallbacks {
        private int m_Injections;

        public int Injections => m_Injections;

        public void OnServicesInitialized() => Interlocked.Increment(ref m_Injections);
    }

    internal sealed class BasedProbe : BaseMonoBehaviour, IPostInitializationCallbacks {
        private int m_Injections;

        public int Injections => m_Injections;

        public void OnServicesInitialized() => Interlocked.Increment(ref m_Injections);
    }

    internal sealed class RequiresServiceProbe : BaseMonoBehaviour {
        [Inject] internal IMissingService Missing { get; set; } = null!;
    }

    private sealed class EmptyServiceProvider : IServiceProvider {
        public object? GetService(Type serviceType) => null;
    }
}
