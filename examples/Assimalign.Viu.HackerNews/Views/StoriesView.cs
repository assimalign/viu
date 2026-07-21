using System;
using System.Collections.Generic;

using Assimalign.Viu;
using Assimalign.Viu.Router;
using Assimalign.Viu.Browser;
using Assimalign.Viu.Shared;

namespace Assimalign.Viu.HackerNews;

/// <summary>
/// The story-list route (<c>/:feed/:page?</c>) — the C# port of vue-hackernews-2.0's list view. It
/// reads the feed/page from the route, drives <see cref="StoriesStore"/> to load through a
/// route-watching effect (so both the initial mount and every subsequent navigation fetch), and
/// renders a keyed <see cref="TransitionGroup"/> list plus pagination. All list state comes from the
/// store; the view holds none of its own.
/// </summary>
internal sealed class StoriesView : IComponent
{
    /// <summary>The shared route-view definition instance.</summary>
    public static readonly StoriesView Instance = new();

    private StoriesView()
    {
    }

    /// <inheritdoc />
    public string? Name => "StoriesView";

    /// <inheritdoc />
    public ComponentSetup Setup(ComponentProperties properties, ComponentSetupContext context)
    {
        var router = DependencyInjection.Inject(RouterInjectionKeys.Router)!;
        var stores = DependencyInjection.GetRequiredService<HackerNewsStores>();
        var store = stores.Stories.UseStore();

        (string Slug, int Page) ReadRouteKey()
        {
            var route = router.CurrentRoute.Value;
            var slug = route.Parameters.TryGetString("feed", out var value) ? value : "top";
            var page = route.Parameters.TryGetInteger("page", out var parsed) && parsed > 0 ? parsed : 1;
            return (slug, page);
        }

        // One effect owns loading: fires immediately (initial mount) and again whenever the feed/page
        // route key changes (client-side navigation). Scoped to this component, so it stops on unmount.
        Reactive.Watch(
            ReadRouteKey,
            (now, _, _) =>
            {
                if (StoryFeeds.TryParse(now.Slug, out var feed))
                {
                    _ = store.LoadPageAsync(feed, now.Page);
                }
            },
            new WatchOptions { Immediate = true });

        return () =>
        {
            var key = ReadRouteKey();
            if (!StoryFeeds.TryParse(key.Slug, out var feed))
            {
                return UnknownFeed(key.Slug);
            }

            var state = store.State;
            VirtualNode content;
            if (state.Error is not null)
            {
                content = ErrorView.Inline(state.Error);
            }
            else if (state.IsLoading && state.Items.Count == 0)
            {
                content = VirtualNodeFactory.Component(LoadingView.Instance);
            }
            else if (state.Items.Count == 0)
            {
                content = Ui.Text("p", "hn-empty", "No stories here right now.");
            }
            else
            {
                content = VirtualNodeFactory.Element(
                    "div",
                    VirtualNodeFactory.Properties(("class", "hn-stories-body")),
                    StoryList(state.Items, key.Page),
                    Pagination(feed, key.Page, store.PageCount.Value));
            }

            return VirtualNodeFactory.Element(
                "section",
                VirtualNodeFactory.Properties(("class", "hn-view hn-stories-view")),
                Ui.Text("h1", "hn-view-title", $"{StoryFeeds.ToLabel(feed)} stories"),
                content);
        };
    }

    // A keyed list under TransitionGroup: each row is keyed by story id so reorders animate (FLIP) and
    // rows reconcile by identity. SlotFlags.Dynamic marks the v-for-style slot so structural changes
    // force the group to re-render its children.
    private static VirtualNode StoryList(IReadOnlyList<HackerNewsItem> items, int page)
    {
        var slots = new ComponentSlots(SlotFlags.Dynamic);
        slots["default"] = _ =>
        {
            var children = new VirtualNode?[items.Count];
            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                var rank = ((page - 1) * StoriesStore.PageSize) + index + 1;
                children[index] = VirtualNodeFactory.Component(
                    StoryItem.Instance,
                    VirtualNodeFactory.Properties(("key", item.Id), ("story", item), ("rank", rank)));
            }
            return children;
        };
        return VirtualNodeFactory.Component(
            TransitionGroup.Instance,
            VirtualNodeFactory.Properties(("tag", "ol"), ("name", "story"), ("class", "hn-stories")),
            slots);
    }

    private static VirtualNode Pagination(StoryFeed feed, int page, int pageCount)
    {
        var boundedPageCount = pageCount < 1 ? 1 : pageCount;
        var children = new List<VirtualNode?>(3)
        {
            page > 1
                ? Ui.Link(Ui.FeedPath(feed, page - 1), "hn-page-link", "‹ prev")
                : Ui.Text("span", "hn-page-link hn-disabled", "‹ prev"),
            Ui.Text("span", "hn-page-info", $"page {page} / {boundedPageCount}"),
            page < boundedPageCount
                ? Ui.Link(Ui.FeedPath(feed, page + 1), "hn-page-link", "more ›")
                : Ui.Text("span", "hn-page-link hn-disabled", "more ›"),
        };
        return VirtualNodeFactory.Element("nav", VirtualNodeFactory.Properties(("class", "hn-pagination")), children.ToArray());
    }

    private static VirtualNode UnknownFeed(string slug)
        => VirtualNodeFactory.Element(
            "section",
            VirtualNodeFactory.Properties(("class", "hn-view")),
            Ui.Text("h1", "hn-view-title", "Not found"),
            Ui.Text("p", "hn-empty", $"There is no “{slug}” feed."),
            Ui.Link("/top", "hn-back-link", "← Back to top stories"));
}
