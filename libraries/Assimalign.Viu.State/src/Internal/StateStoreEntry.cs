using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State;

internal sealed class StateStoreEntry
{
    internal StateStoreEntry(object definition, object instance, IReactiveEffectScope scope)
    {
        Definition = definition;
        Instance = instance;
        Scope = scope;
    }

    internal object Definition { get; }

    internal object Instance { get; }

    internal IReactiveEffectScope Scope { get; }
}
