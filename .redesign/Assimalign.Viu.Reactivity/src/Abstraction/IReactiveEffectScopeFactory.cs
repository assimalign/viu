namespace Assimalign.Viu.Reactivity;

/// <summary>Creates reactive effect scopes without exposing the engine's concrete implementation.</summary>
public interface IReactiveEffectScopeFactory
{
    /// <summary>Creates a reactive effect scope.</summary>
    /// <param name="isDetached">Whether the scope is detached from the ambient parent.</param>
    /// <returns>The new scope.</returns>
    IReactiveEffectScope Create(bool isDetached = false);
}
