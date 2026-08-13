namespace Assimalign.Viu.Reactivity;

/// <summary>
/// Creates first-party reactive effect scopes for abstraction-facing consumers. Specified by
/// <c>[RCT-10]</c>.
/// </summary>
public sealed class ReactiveEffectScopeFactory : IReactiveEffectScopeFactory
{
    /// <inheritdoc />
    public IReactiveEffectScope Create(bool isDetached = false)
        => new EffectScope(isDetached);
}
