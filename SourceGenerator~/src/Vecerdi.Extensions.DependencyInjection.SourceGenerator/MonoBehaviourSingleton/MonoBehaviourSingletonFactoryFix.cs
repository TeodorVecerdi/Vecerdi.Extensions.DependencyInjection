namespace Vecerdi.Extensions.DependencyInjection.SourceGenerator;

/// <summary>How a flagged registration can be rewritten, if at all.</summary>
internal enum MonoBehaviourSingletonFactoryFix {
    /// <summary>The factory does more than resolve the singleton; the diagnostic stands alone.</summary>
    None,

    /// <summary>The factory returns the resolved singleton itself: <c>AddMonoBehaviourSingleton&lt;TService, T&gt;()</c>.</summary>
    Register,

    /// <summary>The factory returns something built from the resolved singleton alone: the selector overload.</summary>
    Select,
}
