using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Vecerdi.Extensions.DependencyInjection.Tests;

// Runs a whole session (build, start, teardown) without play mode. Nothing here registers logging, which is the point:
// logging is optional.
[TestFixture]
public sealed class ServiceManagerTests {
    private static readonly TimeSpan s_Timeout = TimeSpan.FromSeconds(5);

    [Test]
    public void HostedServices_StartAndStop_WithoutLoggingRegistered() {
        // Arrange
        var hostedService = new RecordingHostedService();
        ServiceManager.RegisterServices((services, _) => services.AddSingleton<IHostedService>(hostedService));
        var manager = new ServiceManager();
        // Act
        manager.InitializeServiceManager();
        var started = manager.Lifetime.ApplicationStarted.WaitHandle.WaitOne(s_Timeout);
        manager.TearDownServiceManager();
        // Assert
        Assert.That(started, Is.True);
        Assert.That(hostedService.Started, Is.True);
        Assert.That(hostedService.Stopped, Is.True);
    }

    [Test]
    public void FailingHostedService_WithoutLoggingRegistered_GoesToTheConsole() {
        // Arrange
        var healthy = new RecordingHostedService();
        ServiceManager.RegisterServices((services, _) => {
            services.AddSingleton<IHostedService>(new FailingHostedService());
            services.AddSingleton<IHostedService>(healthy);
        });
        var manager = new ServiceManager();
        LogAssert.Expect(LogType.Exception, new Regex("start boom"));
        LogAssert.Expect(LogType.Exception, new Regex("stop boom"));
        // Act
        manager.InitializeServiceManager();
        manager.Lifetime.ApplicationStarted.WaitHandle.WaitOne(s_Timeout);
        manager.TearDownServiceManager();
        // Assert
        Assert.That(healthy.Started, Is.True);
        Assert.That(healthy.Stopped, Is.True);
    }

    private sealed class RecordingHostedService : IHostedService {
        public bool Started { get; private set; }
        public bool Stopped { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken) {
            Started = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) {
            Stopped = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FailingHostedService : IHostedService {
        public Task StartAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("start boom");
        public Task StopAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("stop boom");
    }
}
