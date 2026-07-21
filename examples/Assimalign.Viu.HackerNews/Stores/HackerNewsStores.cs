using System;

using Assimalign.Viu;
using Assimalign.Viu.Store;

namespace Assimalign.Viu.HackerNews;

/// <summary>
/// The app's store definitions, built once over an injected <see cref="IHackerNewsClient"/> and
/// registered as an <b>application service</b> ([V01.01.03.24]). This is how the injectable data client
/// reaches the stores while keeping <c>UseStore()</c> component-context resolution intact: the browser
/// app registers one over the live client (<c>builder.Services.AddSingleton(stores)</c>), tests register
/// one over a fake, and the views resolve it with <c>DependencyInjection.GetRequiredService&lt;HackerNewsStores&gt;()</c>.
/// This is the reshape's app-level singleton wiring migrated from component-tree provide/inject to
/// <see cref="IServiceProvider"/>. Each <see cref="StoreDefinition{TStore}"/> is resolved lazily and
/// once per registry.
/// </summary>
internal sealed class HackerNewsStores
{
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
