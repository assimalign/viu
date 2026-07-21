using System;
using System.Collections.Generic;

using Assimalign.Viu;

namespace Assimalign.Viu.HackerNews;

/// <summary>
/// One row in a story list — the C# port of vue-hackernews-2.0's <c>StoryItem.vue</c>. A shared,
/// stateless definition (see <see cref="Ui"/>): the parent renders one vnode per story keyed by
/// <see cref="HackerNewsItem.Id"/>, so the keyed diff reconciles rows by identity with no per-item
/// interop, leaving room for list virtualization later (#103). Its props are declared so the story
/// object and rank never fall through as DOM attributes.
/// </summary>
internal sealed class StoryItem : IComponentDefinition
{
    /// <summary>The shared row definition instance.</summary>
    public static readonly StoryItem Instance = new();

    private StoryItem()
    {
    }

    /// <inheritdoc />
    public string? Name => "StoryItem";

    /// <inheritdoc />
    public IReadOnlyList<ComponentPropertyDefinition>? Properties { get; } =
    [
        new ComponentPropertyDefinition("story") { Required = true },
        new ComponentPropertyDefinition("rank") { DefaultValue = 0 },
    ];

    /// <inheritdoc />
    public Func<VirtualNode?> Setup(ComponentProperties properties, ComponentSetupContext context)
        => () =>
        {
            var story = properties.Get<HackerNewsItem>("story");
            if (story is null)
            {
                return VirtualNodeFactory.Comment();
            }
            var rank = properties.Get<int>("rank");
            var itemPath = $"/item/{story.Id}";

            return VirtualNodeFactory.Element(
                "li",
                VirtualNodeFactory.Properties(("class", "hn-story")),
                Ui.Text("span", "hn-rank", rank > 0 ? $"{rank}." : string.Empty),
                VirtualNodeFactory.Element(
                    "div",
                    VirtualNodeFactory.Properties(("class", "hn-story-body")),
                    Title(story, itemPath),
                    Subtext(story, itemPath)));
        };

    private static VirtualNode Title(HackerNewsItem story, string itemPath)
    {
        var titleText = story.Title ?? "(untitled)";
        // A story with an external URL links out (new tab) and shows its host; an Ask/Show/self post
        // links to its own discussion page.
        var titleLink = story.Url is { Length: > 0 } url
            ? Ui.ExternalLink(url, "hn-story-title", titleText)
            : Ui.Link(itemPath, "hn-story-title", titleText);

        if (story.Host is not { Length: > 0 } host)
        {
            return VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("class", "hn-story-head")), titleLink);
        }
        return VirtualNodeFactory.Element(
            "div",
            VirtualNodeFactory.Properties(("class", "hn-story-head")),
            titleLink,
            Ui.Text("span", "hn-story-host", $" ({host})"));
    }

    private static VirtualNode Subtext(HackerNewsItem story, string itemPath)
    {
        var parts = new List<VirtualNode?>(6);
        var isJob = string.Equals(story.Type, "job", StringComparison.Ordinal);

        if (!isJob)
        {
            parts.Add(Ui.Text("span", "hn-points", Ui.Plural(story.Score, "point")));
            parts.Add(Ui.Raw(" by "));
            parts.Add(Ui.Link($"/user/{story.By}", "hn-by", story.By ?? "unknown"));
            parts.Add(Ui.Raw(" "));
        }
        parts.Add(Ui.Raw(Ui.TimeAgo(story.Time)));
        if (!isJob)
        {
            parts.Add(Ui.Raw(" | "));
            parts.Add(Ui.Link(itemPath, "hn-comments-link", Ui.Plural(story.Descendants, "comment")));
        }

        return VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("class", "hn-subtext")), parts.ToArray());
    }
}
