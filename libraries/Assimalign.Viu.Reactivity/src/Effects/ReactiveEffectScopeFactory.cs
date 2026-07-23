namespace Assimalign.Viu.Reactivity;

/// <summary>Creates first-party reactive effect scopes for abstraction-facing consumers.</summary>
public sealed class ReactiveEffectScopeFactory : IReactiveEffectScopeFactory
{
    /// <inheritdoc />
    public IReactiveEffectScope Create(bool isDetached = false)
        => new EffectScope(isDetached);
}
