namespace Vecerdi.Extensions.Lifetime;

/// <summary>Why the code lifetime ended.</summary>
public enum CodeLifetimeEnd {
    /// <summary>The editor is reloading code (a recompile, or entering play mode with domain reload on). The process lives on.</summary>
    Reload,

    /// <summary>The editor or the player is quitting.</summary>
    Quit,
}
