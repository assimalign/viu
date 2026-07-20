using System;

using Assimalign.Viu.RuntimeCore;
using Assimalign.Viu.Store;

namespace Assimalign.Viu.HackerNews;

/// <summary>
/// The app's store definitions, built once over an injected <see cref="IHackerNewsClient"/> and
/// provided app-wide under <see cref="InjectionKey"/>. This is how the injectable data client reaches
/// the stores while keeping <c>UseStore()</c> component-context resolution intact: the browser app
/// builds one over the live client, tests build one over a fake, and both provide it to the tree.
/// Each <see cref="StoreDefinition{TStore}"/> is resolved lazily and once per registry.
/// </summary>
internal sealed class HackerNewsStores
{
    /// <summary>The provide/inject key components resolve the store definitions under.</summary>
    public static readonly InjectionKey<HackerNewsStores> InjectionKey = new("hn:stores");

    /// <summary>Creates the definitions over <paramref name="client"/>.</summary>
    /// <param name="client">The data client every store reads through.</param>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> is null.</exception>
    public HackerNewsStores(IHackerNewsClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        Stories = Assimalign.Viu.Store.Stores.DefineStore("stories", () => new StoriesStore(client));
        Item = Assimalign.Viu.Store.Stores.DefineStore("item", () => new ItemStore(client));
        User = Assimalign.Viu.Store.Stores.DefineStore("user", () => new UserStore(client));
    }

    /// <summary>The paged story-list store definition.</summary>
    public StoreDefinition<StoriesStore> Stories { get; }

    /// <summary>The item-detail store definition.</summary>
    public StoreDefinition<ItemStore> Item { get; }

    /// <summary>The user-profile store definition.</summary>
    public StoreDefinition<UserStore> User { get; }
}
