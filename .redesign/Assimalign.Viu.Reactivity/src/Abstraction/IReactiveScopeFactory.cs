namespace Assimalign.Viu.Reactivity;

/// <summary>Creates reactive scopes without exposing the engine's concrete scope implementation.</summary>
public interface IReactiveScopeFactory
{
    /// <summary>Creates a reactive scope.</summary>
    /// <param name="isDetached">Whether the scope is detached from the ambient parent.</param>
    /// <returns>The new scope.</returns>
    IReactiveScope Create(bool isDetached = false);
}

