using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Vecerdi.Extensions.DependencyInjection.Infrastructure;
using Vecerdi.Extensions.DependencyInjection.Internal;
using Object = UnityEngine.Object;

namespace Vecerdi.Extensions.DependencyInjection.Tests;

[TestFixture]
public sealed class TypeInjectorCacheTests {
    private readonly List<GameObject> m_Objects = [];

    [TearDown]
    public void TearDown() {
        foreach (var gameObject in m_Objects) {
            if (gameObject != null) Object.DestroyImmediate(gameObject);
        }

        m_Objects.Clear();
    }

    [Test]
    public void InjectingSeveralInstancesOfOneType_ResolvesItsInjectorOnce() {
        // Arrange
        var resolver = new CountingResolver(new ReflectionTypeInjectorResolver());
        // Act
        for (var i = 0; i < 3; i++) {
            BehaviourServices.InjectIntoMonoBehaviourProperties(new EmptyServiceProvider(), resolver, Place<FirstProbe>());
        }

        BehaviourServices.InjectIntoMonoBehaviourProperties(new EmptyServiceProvider(), resolver, Place<SecondProbe>());
        // Assert
        Assert.That(resolver.Lookups, Is.EqualTo(new[] { typeof(FirstProbe), typeof(SecondProbe) }));
    }

    [Test]
    public void ADifferentResolver_ResolvesAgain() {
        // Arrange
        var first = new CountingResolver(new ReflectionTypeInjectorResolver());
        var second = new CountingResolver(new ReflectionTypeInjectorResolver());
        BehaviourServices.InjectIntoMonoBehaviourProperties(new EmptyServiceProvider(), first, Place<FirstProbe>());
        // Act
        BehaviourServices.InjectIntoMonoBehaviourProperties(new EmptyServiceProvider(), second, Place<FirstProbe>());
        BehaviourServices.InjectIntoMonoBehaviourProperties(new EmptyServiceProvider(), second, Place<FirstProbe>());
        // Assert
        Assert.That(first.Lookups, Has.Count.EqualTo(1));
        Assert.That(second.Lookups, Has.Count.EqualTo(1));
    }

    [Test]
    public void ATypeWithoutAnInjector_IsResolvedAgainEachTime() {
        // Arrange
        var resolver = new CountingResolver(null);
        // Act
        Assert.Throws<InvalidOperationException>(() => BehaviourServices.InjectIntoMonoBehaviourProperties(new EmptyServiceProvider(), resolver, Place<FirstProbe>()));
        Assert.Throws<InvalidOperationException>(() => BehaviourServices.InjectIntoMonoBehaviourProperties(new EmptyServiceProvider(), resolver, Place<FirstProbe>()));
        // Assert
        Assert.That(resolver.Lookups, Has.Count.EqualTo(2));
    }

    private T Place<T>() where T : MonoBehaviour {
        var gameObject = new GameObject(typeof(T).Name);
        m_Objects.Add(gameObject);
        return gameObject.AddComponent<T>();
    }

    internal sealed class FirstProbe : BaseMonoBehaviour;

    internal sealed class SecondProbe : BaseMonoBehaviour;

    private sealed class CountingResolver(ITypeInjectorResolver? inner) : ITypeInjectorResolver {
        public List<Type> Lookups { get; } = [];

        public ITypeInjector? GetTypeInjector(Type type) {
            Lookups.Add(type);
            return inner?.GetTypeInjector(type);
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider {
        public object? GetService(Type serviceType) => null;
    }
}
