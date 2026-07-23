using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State;

internal sealed class StateStoreEntry
{
    internal StateStoreEntry(object definition, object store, IReactiveScope scope)
    {
        Definition = definition;
        Store = store;
        Scope = scope;
    }

    internal object Definition { get; }

    internal object Store { get; }

    internal IReactiveScope Scope { get; }
}

