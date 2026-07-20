namespace Assimalign.Viu.Store;

/// <summary>
/// The metadata handed to a <see cref="Store{TState}.Subscribe"/> callback describing one flush of
/// store mutations — the C# port of the mutation object Pinia's <c>$subscribe</c> callback receives
/// (https://pinia.vuejs.org/core-concepts/state.html#Subscribing-to-the-state,
/// <c>packages/pinia/src/types.ts</c> <c>SubscriptionCallbackMutation</c>). It names the
/// <see cref="StoreId"/> that changed and the <see cref="Kind"/> of change (direct write versus a
/// grouped patch), so a single subscription can serve many stores and branch on how the change
/// arrived. A readonly value type: it is never mutated after delivery and never boxes when passed to
/// a typed <see cref="Delegates.StoreSubscriptionCallback{TState}"/>.
/// </summary>
public readonly struct StoreMutation
{
    /// <summary>Creates a mutation record for <paramref name="storeId"/> and <paramref name="kind"/>.</summary>
    /// <param name="storeId">The id of the store whose state changed.</param>
    /// <param name="kind">How the change reached subscribers.</param>
    public StoreMutation(string storeId, StorePatchKind kind)
    {
        StoreId = storeId;
        Kind = kind;
    }

    /// <summary>The id of the store whose state changed (upstream: <c>mutation.storeId</c>).</summary>
    public string StoreId { get; }

    /// <summary>How the change reached subscribers (upstream: <c>mutation.type</c>).</summary>
    public StorePatchKind Kind { get; }
}
