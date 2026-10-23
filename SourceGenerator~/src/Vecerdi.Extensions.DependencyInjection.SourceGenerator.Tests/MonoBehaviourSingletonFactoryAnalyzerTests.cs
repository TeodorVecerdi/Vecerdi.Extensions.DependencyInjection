namespace Vecerdi.Extensions.DependencyInjection.SourceGenerator.Tests;

using static AnalyzerHarness;

[TestClass]
public sealed class MonoBehaviourSingletonFactoryAnalyzerTests {
    private const string Prelude = /*lang=csharp*/
        """
        using System;
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.Extensions.DependencyInjection.Extensions;
        using Vecerdi.Extensions.DependencyInjection;

        namespace Game;

        public interface IAudio { }
        public interface IMixer { }
        public sealed class Mixer : IMixer { }
        public sealed class Clock { }

        public sealed class AudioDirector : MonoSingleton<AudioDirector>, IAudio {
            public IMixer Mixer { get; } = new Mixer();
            public IMixer GetChannel(object? key) => Mixer;
        }

        public sealed class PlainDirector : UnityEngine.MonoBehaviour, IAudio { }

        """;

    private static string Registrations(string body) => Prelude + $$"""
        public static class Registrations {
            public static void Register(IServiceCollection services) {
                {{body}}
            }
        }

        """;

    private static async Task AssertReports(string body, string expectedMessage) {
        var diagnostics = await GetDiagnosticsAsync(Registrations(body));
        Assert.HasCount(1, diagnostics);
        Assert.AreEqual("VDI0008", diagnostics[0].Id);
        Assert.AreEqual(expectedMessage, diagnostics[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static async Task AssertNothingReported(string body) {
        var diagnostics = await GetDiagnosticsAsync(Registrations(body));
        Assert.IsEmpty(diagnostics, string.Join("\n", diagnostics));
    }

    private static async Task AssertFix(string body, string expectedBody) {
        var fixedSource = await ApplyFixAsync(Registrations(body));
        Assert.IsNotNull(fixedSource, "The diagnostic offered no fix.");
        Assert.AreEqual(Registrations(expectedBody).Replace("\r\n", "\n"), fixedSource.Replace("\r\n", "\n"));
    }

    private static string Message(string type, string lifetime) =>
        $"'{type}' is a MonoBehaviour singleton, and this {lifetime} registration caches what its factory resolves from it, which goes stale once Unity destroys the instance; register it with AddMonoBehaviourSingleton instead";

    // Detection

    [TestMethod]
    public Task SingletonFactory_ResolvingAMonoSingleton_IsReported() =>
        AssertReports("services.AddSingleton<IAudio>(sp => sp.GetRequiredService<AudioDirector>());", Message("AudioDirector", "singleton"));

    [TestMethod]
    public Task ScopedFactory_IsReported() =>
        AssertReports("services.AddScoped<IAudio>(sp => sp.GetRequiredService<AudioDirector>());", Message("AudioDirector", "scoped"));

    [TestMethod]
    public Task TryAddFactory_IsReported() =>
        AssertReports("services.TryAddSingleton<IAudio>(sp => sp.GetRequiredService<AudioDirector>());", Message("AudioDirector", "singleton"));

    [TestMethod]
    public Task KeyedFactory_IsReported() =>
        AssertReports("services.AddKeyedSingleton<IAudio>(\"main\", (sp, _) => sp.GetRequiredService<AudioDirector>());", Message("AudioDirector", "singleton"));

    [TestMethod]
    public Task GetService_IsReported() =>
        AssertReports("services.AddSingleton<IAudio>(sp => sp.GetService<AudioDirector>()!);", Message("AudioDirector", "singleton"));

    [TestMethod]
    public Task Projection_IsReported() =>
        AssertReports("services.AddSingleton<IMixer>(sp => sp.GetRequiredService<AudioDirector>().Mixer);", Message("AudioDirector", "singleton"));

    [TestMethod]
    public Task TransientFactory_IsNotReported() =>
        AssertNothingReported("services.AddTransient<IAudio>(sp => sp.GetRequiredService<AudioDirector>());");

    [TestMethod]
    public Task PlainMonoBehaviour_IsNotReported() =>
        AssertNothingReported("services.AddSingleton<IAudio>(sp => sp.GetRequiredService<PlainDirector>());");

    [TestMethod]
    public Task ResolveThroughAnInterface_IsNotReported() =>
        AssertNothingReported("services.AddSingleton<IMixer>(sp => new Mixer()); services.AddSingleton<IMixer>(sp => sp.GetRequiredService<IMixer>());");

    [TestMethod]
    public Task OrdinaryFactory_IsNotReported() =>
        AssertNothingReported("services.AddSingleton<Clock>(sp => new Clock());");

    // Fixes

    [TestMethod]
    public Task Fix_Forward_ToTheServiceAndSingletonOverload() =>
        AssertFix(
            "services.AddSingleton<IAudio>(sp => sp.GetRequiredService<AudioDirector>());",
            "services.AddMonoBehaviourSingleton<IAudio, AudioDirector>();");

    [TestMethod]
    public Task Fix_ForwardOfTheSingletonItself_ToTheSingleTypeOverload() =>
        AssertFix(
            "services.AddSingleton<AudioDirector>(sp => sp.GetRequiredService<AudioDirector>());",
            "services.AddMonoBehaviourSingleton<AudioDirector>();");

    [TestMethod]
    public Task Fix_Projection_ToTheSelectorOverload() =>
        AssertFix(
            "services.AddSingleton<IMixer>(sp => sp.GetRequiredService<AudioDirector>().Mixer);",
            "services.AddMonoBehaviourSingleton<IMixer, AudioDirector>(audioDirector => audioDirector.Mixer);");

    [TestMethod]
    public Task Fix_KeyedForward_KeepsTheKey() =>
        AssertFix(
            "services.AddKeyedSingleton<IAudio>(\"main\", (sp, _) => sp.GetRequiredService<AudioDirector>());",
            "services.AddKeyedMonoBehaviourSingleton<IAudio, AudioDirector>(\"main\");");

    [TestMethod]
    public Task Fix_KeyedProjection_PassesTheKeyThrough() =>
        AssertFix(
            "services.AddKeyedSingleton<IMixer>(\"main\", (sp, key) => sp.GetRequiredService<AudioDirector>().GetChannel(key));",
            "services.AddKeyedMonoBehaviourSingleton<IMixer, AudioDirector>(\"main\", (audioDirector, key) => audioDirector.GetChannel(key));");

    [TestMethod]
    public Task Fix_ParameterName_AvoidsACollision() =>
        AssertFix(
            "var audioDirector = 1; services.AddSingleton<IMixer>(sp => sp.GetRequiredService<AudioDirector>().Mixer);",
            "var audioDirector = 1; services.AddMonoBehaviourSingleton<IMixer, AudioDirector>(audioDirector2 => audioDirector2.Mixer);");

    [TestMethod]
    public async Task Fix_AddsTheUsingWhenMissing() {
        var source = Registrations("services.AddSingleton<IAudio>(sp => sp.GetRequiredService<AudioDirector>());")
            .Replace("using Vecerdi.Extensions.DependencyInjection;\n", "", StringComparison.Ordinal)
            .Replace("MonoSingleton<AudioDirector>", "Vecerdi.Extensions.DependencyInjection.MonoSingleton<AudioDirector>", StringComparison.Ordinal);
        var fixedSource = await ApplyFixAsync(source);
        Assert.IsNotNull(fixedSource);
        Assert.Contains("using Microsoft.Extensions.DependencyInjection.Extensions;\nusing Vecerdi.Extensions.DependencyInjection;\n", fixedSource.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public async Task FactoryResolvingOtherServices_HasNoFix() {
        var fixedSource = await ApplyFixAsync(Registrations("services.AddSingleton<IMixer>(sp => sp.GetRequiredService<Clock>() is null ? new Mixer() : sp.GetRequiredService<AudioDirector>().Mixer);"));
        Assert.IsNull(fixedSource);
    }
}
