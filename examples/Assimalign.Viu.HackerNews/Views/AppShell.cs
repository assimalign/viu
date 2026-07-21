using System;
using System.Collections.Generic;

using Assimalign.Viu;

namespace Assimalign.Viu.HackerNews;

/// <summary>
/// The application root: the HackerNews-style header (logo + feed tabs, all
/// <see cref="Router.RouterLink"/>s so navigation is client-side through the router) wrapped around
/// the single <see cref="Router.RouterView"/> outlet that renders the matched route. The whole page
/// chrome mirrors vuejs/vue-hackernews-2.0's <c>App.vue</c>.
/// </summary>
internal sealed class AppShell : IComponentDefinition
{
    /// <summary>The shared root definition instance.</summary>
    public static readonly AppShell Instance = new();

    private AppShell()
    {
    }

    /// <inheritdoc />
    public string? Name => "AppShell";

    /// <inheritdoc />
    public Func<VirtualNode?> Setup(ComponentProperties properties, ComponentSetupContext context)
        => () => VirtualNodeFactory.Element(
            "div",
            VirtualNodeFactory.Properties(("class", "hn-app")),
            Header(),
            VirtualNodeFactory.Element(
                "main",
                VirtualNodeFactory.Properties(("class", "hn-main")),
                VirtualNodeFactory.Component(Ui.RouterView)),
            Ui.Text("footer", "hn-footer", "Built with Viu — a C#/.NET port of Vue 3. Data from the HackerNews API."));

    private static VirtualNode Header()
    {
        var tabs = new List<VirtualNode?>(StoryFeeds.All.Length);
        foreach (var feed in StoryFeeds.All)
        {
            tabs.Add(Ui.Link(Ui.FeedPath(feed, 1), "hn-tab", StoryFeeds.ToLabel(feed)));
        }

        return VirtualNodeFactory.Element(
            "header",
            VirtualNodeFactory.Properties(("class", "hn-header")),
            // The logo links to "/" so clicking it exercises the BeforeEach root-redirect guard (→ /top).
            Ui.Link("/", "hn-logo", VirtualNodeFactory.Element("span", VirtualNodeFactory.Properties(("class", "hn-logo-mark")), "Y"), VirtualNodeFactory.Text(" Viu HN")),
            VirtualNodeFactory.Element("nav", VirtualNodeFactory.Properties(("class", "hn-tabs")), tabs.ToArray()));
    }
}
