using System;
using System.Collections.Generic;

using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Router;
using Assimalign.Viu.RuntimeCore;

namespace Assimalign.Viu.HackerNews;

/// <summary>
/// The item-detail route (<c>/item/:id</c>) — the C# port of vue-hackernews-2.0's item view. Reads
/// <c>:id</c> from the route, drives <see cref="ItemStore"/> through a route-watching effect, and
/// renders the story header plus the recursive comment tree (<see cref="CommentView"/>). All state
/// comes from the store. Wrapped as an async route component in <see cref="AppRoutes"/>.
/// </summary>
internal sealed class ItemView : IComponentDefinition
{
    /// <summary>The shared route-view definition instance.</summary>
    public static readonly ItemView Instance = new();

    private ItemView()
    {
    }

    /// <inheritdoc />
    public string? Name => "ItemView";

    /// <inheritdoc />
    public Func<VirtualNode?> Setup(ComponentProperties properties, ComponentSetupContext context)
    {
        var router = DependencyInjection.Inject(RouterInjectionKeys.Router)!;
        var stores = DependencyInjection.Inject(HackerNewsStores.InjectionKey)!;
        var store = stores.Item.UseStore();

        long ReadId() => router.CurrentRoute.Value.Parameters.TryGetInteger("id", out var id) ? id : 0;

        Reactive.Watch(
            ReadId,
            (id, _, _) =>
            {
                if (id > 0)
                {
                    _ = store.LoadItemAsync(id);
                }
            },
            new WatchOptions { Immediate = true });

        return () =>
        {
            var state = store.State;
            if (state.Error is not null)
            {
                return View(ErrorView.Inline(state.Error));
            }
            if (state.Story is null)
            {
                return View(VirtualNodeFactory.Component(LoadingView.Instance));
            }
            return View(Header(state.Story), Comments(state.Comments, state.Story));
        };
    }

    private static VirtualNode View(params VirtualNode?[] children)
        => VirtualNodeFactory.Element("section", VirtualNodeFactory.Properties(("class", "hn-view hn-item-view")), children);

    private static VirtualNode Header(HackerNewsItem story)
    {
        var titleText = story.Title ?? "(untitled)";
        var titleNode = story.Url is { Length: > 0 } url
            ? Ui.ExternalLink(url, "hn-item-title", titleText)
            : Ui.Text("span", "hn-item-title", titleText);

        var head = new List<VirtualNode?>(2) { titleNode };
        if (story.Host is { Length: > 0 } host)
        {
            head.Add(Ui.Text("span", "hn-story-host", $" ({host})"));
        }

        var meta = Ui.Text(
            "div",
            "hn-item-meta",
            $"{Ui.Plural(story.Score, "point")} by {story.By ?? "unknown"} · {Ui.TimeAgo(story.Time)} · {Ui.Plural(story.Descendants, "comment")}");

        var children = new List<VirtualNode?>(3)
        {
            VirtualNodeFactory.Element("h1", VirtualNodeFactory.Properties(("class", "hn-item-head")), head.ToArray()),
            meta,
        };

        // Ask HN / self posts carry their body in Text.
        var paragraphs = HtmlText.ToParagraphs(story.Text);
        if (paragraphs.Count > 0)
        {
            var bodyNodes = new VirtualNode?[paragraphs.Count];
            for (var index = 0; index < paragraphs.Count; index++)
            {
                bodyNodes[index] = Ui.Text("p", null, paragraphs[index]);
            }
            children.Add(VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("class", "hn-item-text")), bodyNodes));
        }

        return VirtualNodeFactory.Element("header", VirtualNodeFactory.Properties(("class", "hn-item-header")), children.ToArray());
    }

    private static VirtualNode Comments(IReadOnlyList<CommentNode> comments, HackerNewsItem story)
    {
        var heading = Ui.Text("h2", "hn-comments-heading", Ui.Plural(story.Descendants, "comment"));
        if (comments.Count == 0)
        {
            return VirtualNodeFactory.Element(
                "div",
                VirtualNodeFactory.Properties(("class", "hn-comments-section")),
                heading,
                Ui.Text("p", "hn-empty", "No comments yet."));
        }

        var items = new VirtualNode?[comments.Count];
        for (var index = 0; index < comments.Count; index++)
        {
            var node = comments[index];
            items[index] = VirtualNodeFactory.Component(
                CommentView.Instance,
                VirtualNodeFactory.Properties(("key", node.Comment.Id), ("node", node)));
        }

        return VirtualNodeFactory.Element(
            "div",
            VirtualNodeFactory.Properties(("class", "hn-comments-section")),
            heading,
            VirtualNodeFactory.Element("ul", VirtualNodeFactory.Properties(("class", "hn-comments")), items));
    }
}
