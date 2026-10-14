using System;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Vecerdi.Extensions.DependencyInjection.Internal;

namespace Vecerdi.Extensions.DependencyInjection.Tests;

[TestFixture]
public sealed class HostApplicationLifetimeTests {
    [Test]
    public void Notifications_CancelTheirTokens() {
        // Arrange
        var lifetime = new HostApplicationLifetime(null);
        // Act
        lifetime.NotifyStarted();
        lifetime.NotifyStopping();
        lifetime.NotifyStopped();
        // Assert
        Assert.That(lifetime.ApplicationStarted.IsCancellationRequested, Is.True);
        Assert.That(lifetime.ApplicationStopping.IsCancellationRequested, Is.True);
        Assert.That(lifetime.ApplicationStopped.IsCancellationRequested, Is.True);
    }

    [Test]
    public void NotifyStarted_AfterStopping_IsSkipped() {
        // Arrange
        var lifetime = new HostApplicationLifetime(null);
        lifetime.NotifyStopping();
        // Act
        lifetime.NotifyStarted();
        // Assert
        Assert.That(lifetime.ApplicationStarted.IsCancellationRequested, Is.False);
    }

    [Test]
    public void Notify_RunsCallbacksOnce() {
        // Arrange
        var lifetime = new HostApplicationLifetime(null);
        var calls = 0;
        lifetime.ApplicationStopping.Register(() => calls++);
        // Act
        lifetime.NotifyStopping();
        lifetime.NotifyStopping();
        // Assert
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void Notify_ThrowingCallback_IsLoggedAndTheOthersRun() {
        // Arrange
        var lifetime = new HostApplicationLifetime(null);
        var ran = false;
        lifetime.ApplicationStopped.Register(() => ran = true);
        lifetime.ApplicationStopped.Register(() => throw new InvalidOperationException("stopped boom"));
        LogAssert.Expect(LogType.Exception, new Regex("stopped boom"));
        // Act
        lifetime.NotifyStopped();
        // Assert
        Assert.That(ran, Is.True);
    }
}
