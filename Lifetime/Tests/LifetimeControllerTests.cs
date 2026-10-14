using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Vecerdi.Extensions.Lifetime.Tests;

[TestFixture]
public sealed class LifetimeControllerTests {
    private static readonly TimeSpan s_ShortBudget = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan s_LongBudget = TimeSpan.FromSeconds(5);

    [Test]
    public void Stop_CancelsTokenAndRecordsReason() {
        // Arrange
        var controller = new LifetimeController(s_LongBudget);
        // Act
        controller.Stop(CodeLifetimeEnd.Reload);
        // Assert
        Assert.That(controller.Token.IsCancellationRequested, Is.True);
        Assert.That(controller.Ending, Is.EqualTo(CodeLifetimeEnd.Reload));
    }

    [Test]
    public void Stop_RunsCallbacksInReverseRegistrationOrder_WithTheReason() {
        // Arrange
        var controller = new LifetimeController(s_LongBudget);
        var calls = new List<string>();
        controller.Register("first", ending => calls.Add($"first:{ending}"));
        controller.Register("second", ending => calls.Add($"second:{ending}"));
        // Act
        controller.Stop(CodeLifetimeEnd.Quit);
        // Assert
        Assert.That(calls, Is.EqualTo(new[] { "second:Quit", "first:Quit" }));
    }

    [Test]
    public void Stop_CancelsTokenBeforeRunningCallbacks() {
        // Arrange
        var controller = new LifetimeController(s_LongBudget);
        var cancelledWhenCalled = false;
        controller.Register("owner", _ => cancelledWhenCalled = controller.Token.IsCancellationRequested);
        // Act
        controller.Stop(CodeLifetimeEnd.Reload);
        // Assert
        Assert.That(cancelledWhenCalled, Is.True);
    }

    [Test]
    public void Stop_SecondCall_IsIgnored() {
        // Arrange
        var controller = new LifetimeController(s_LongBudget);
        var calls = 0;
        controller.Register("owner", _ => calls++);
        controller.Stop(CodeLifetimeEnd.Quit);
        // Act
        controller.Stop(CodeLifetimeEnd.Reload);
        // Assert
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(controller.Ending, Is.EqualTo(CodeLifetimeEnd.Quit));
    }

    [Test]
    public void Stop_ThrowingCallback_DoesNotPreventTheOthers() {
        // Arrange
        var controller = new LifetimeController(s_LongBudget);
        var ran = false;
        controller.Register("survivor", _ => ran = true);
        controller.Register("thrower", _ => throw new InvalidOperationException("boom"));
        LogAssert.Expect(LogType.Exception, new Regex("boom"));
        // Act
        controller.Stop(CodeLifetimeEnd.Reload);
        // Assert
        Assert.That(ran, Is.True);
    }

    [Test]
    public void Stop_ThrowingTokenRegistration_DoesNotPreventCallbacks() {
        // Arrange
        var controller = new LifetimeController(s_LongBudget);
        var ran = false;
        controller.Token.Register(() => throw new InvalidOperationException("token boom"));
        controller.Register("owner", _ => ran = true);
        LogAssert.Expect(LogType.Exception, new Regex("token boom"));
        // Act
        controller.Stop(CodeLifetimeEnd.Reload);
        // Assert
        Assert.That(ran, Is.True);
    }

    [Test]
    public void Register_AfterStop_RunsImmediatelyWithTheReason() {
        // Arrange
        var controller = new LifetimeController(s_LongBudget);
        controller.Stop(CodeLifetimeEnd.Quit);
        CodeLifetimeEnd? received = null;
        // Act
        controller.Register("late", ending => received = ending);
        // Assert
        Assert.That(received, Is.EqualTo(CodeLifetimeEnd.Quit));
    }

    [Test]
    public void DisposedRegistration_IsNotCalled() {
        // Arrange
        var controller = new LifetimeController(s_LongBudget);
        var called = false;
        var registration = controller.Register("owner", _ => called = true);
        registration.Dispose();
        // Act
        controller.Stop(CodeLifetimeEnd.Reload);
        // Assert
        Assert.That(called, Is.False);
    }

    [Test]
    public void Stop_WaitsForTrackedWork() {
        // Arrange
        var controller = new LifetimeController(s_LongBudget);
        var token = controller.Token;
        var work = Task.Run(() => {
            token.WaitHandle.WaitOne();
            Thread.Sleep(50);
        });
        controller.Track("writer", work);
        // Act
        controller.Stop(CodeLifetimeEnd.Reload);
        // Assert
        Assert.That(work.IsCompleted, Is.True);
        Assert.That(controller.GetStragglers(), Is.Empty);
    }

    [Test]
    public void ReportStragglers_NamesWorkThatOutlivedTheBudget() {
        // Arrange
        var controller = new LifetimeController(s_ShortBudget);
        var never = new TaskCompletionSource<bool>();
        controller.Track("stuck-writer", never.Task);
        controller.Stop(CodeLifetimeEnd.Reload);
        LogAssert.Expect(LogType.Warning, new Regex("'stuck-writer' \\(task\\)"));
        try {
            // Act
            var count = controller.ReportStragglers("At code unload");
            // Assert
            Assert.That(count, Is.EqualTo(1));
        } finally {
            never.SetResult(true);
        }
    }

    [Test]
    public void Stop_JoinsTrackedThreadsThatObserveTheToken() {
        // Arrange
        var controller = new LifetimeController(s_LongBudget);
        var token = controller.Token;
        var thread = new Thread(() => token.WaitHandle.WaitOne()) { Name = "observer", IsBackground = true };
        thread.Start();
        controller.Track("observer-owner", thread);
        // Act
        controller.Stop(CodeLifetimeEnd.Quit);
        // Assert
        Assert.That(thread.IsAlive, Is.False);
    }

    [Test]
    public void ReportStragglers_NamesThreadsStillAlive() {
        // Arrange
        var controller = new LifetimeController(s_ShortBudget);
        using var release = new ManualResetEventSlim(false);
        var thread = new Thread(() => release.Wait()) { Name = "ignorer", IsBackground = true };
        thread.Start();
        controller.Track("ignorer-owner", thread);
        controller.Stop(CodeLifetimeEnd.Reload);
        LogAssert.Expect(LogType.Warning, new Regex("'ignorer-owner' \\(thread 'ignorer'\\)"));
        try {
            // Act
            var count = controller.ReportStragglers("At code unload");
            // Assert
            Assert.That(count, Is.EqualTo(1));
        } finally {
            release.Set();
            thread.Join();
        }
    }

    [Test]
    public void Track_CompletedWork_IsNotReported() {
        // Arrange
        var controller = new LifetimeController(s_ShortBudget);
        controller.Track("done", Task.CompletedTask);
        // Act
        controller.Stop(CodeLifetimeEnd.Reload);
        // Assert
        Assert.That(controller.ReportStragglers("At code unload"), Is.Zero);
    }
}
