using System;
using NUnit.Framework;
using UnityEngine;
using Vecerdi.Extensions.DependencyInjection.Infrastructure;
using Vecerdi.Extensions.DependencyInjection.Internal;
using Object = UnityEngine.Object;

namespace Vecerdi.Extensions.DependencyInjection.Tests;

[TestFixture]
public sealed class InjectionFailureTests {
    private GameObject? m_Root;

    [TearDown]
    public void TearDown() {
        if (m_Root != null) Object.DestroyImmediate(m_Root);
    }

    [Test]
    public void MissingRequiredService_NamesTheComponentAndItsHierarchyPath() {
        // Arrange
        m_Root = new GameObject("Canvas");
        var child = new GameObject("ScoreBoard");
        child.transform.SetParent(m_Root.transform);
        var component = child.AddComponent<ScoreBoardProbe>();
        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            BehaviourServices.InjectIntoMonoBehaviourProperties(new EmptyServiceProvider(), new ReflectionTypeInjectorResolver(), component));
        // Assert
        Assert.That(exception!.Message, Is.EqualTo("Could not inject services into ScoreBoardProbe on 'Canvas/ScoreBoard'."));
        Assert.That(exception.InnerException!.Message, Does.Contain(nameof(IScoreService)).And.Contain(nameof(ScoreBoardProbe.Scores)));
    }

    [Test]
    public void MissingRequiredService_LeavesTheComponentUninjected() {
        // Arrange
        m_Root = new GameObject("ScoreBoard");
        var component = m_Root.AddComponent<ScoreBoardProbe>();
        var provider = new EmptyServiceProvider();
        var resolver = new ReflectionTypeInjectorResolver();
        Assert.Throws<InvalidOperationException>(() => BehaviourServices.InjectIntoMonoBehaviourProperties(provider, resolver, component));
        // Act: a retry runs the injector again instead of being skipped as already injected.
        var retry = Assert.Throws<InvalidOperationException>(() => BehaviourServices.InjectIntoMonoBehaviourProperties(provider, resolver, component));
        // Assert
        Assert.That(retry, Is.Not.Null);
        Assert.That(component.ServicesInjected, Is.False);
    }

    internal interface IScoreService;

    internal sealed class ScoreBoardProbe : BaseMonoBehaviour {
        [Inject] internal IScoreService Scores { get; set; } = null!;
    }

    private sealed class EmptyServiceProvider : IServiceProvider {
        public object? GetService(Type serviceType) => null;
    }
}
