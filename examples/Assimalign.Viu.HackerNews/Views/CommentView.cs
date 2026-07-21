using System;
using System.Collections.Generic;

using Assimalign.Viu;

namespace Assimalign.Viu.HackerNews;

/// <summary>
/// A single node in the comment tree, rendered recursively — the C# port of vue-hackernews-2.0's
/// <c>Comment.vue</c>. A shared, stateless definition that references itself for replies; each node
/// vnode is keyed by its comment id so the tree reconciles by identity. Comment HTML is rendered as
/// safe plain-text paragraphs (see <see cref="HtmlText"/>).
/// </summary>
internal sealed class CommentView : IComponent
{
    /// <summary>The shared comment definition instance (also the recursion target).</summary>
    public static readonly CommentView Instance = new();

    private CommentView()
    {
    }

    /// <inheritdoc />
    public string? Name => "CommentView";

    /// <inheritdoc />
    public IReadOnlyList<ComponentPropertyDefinition>? Properties { get; } =
    [
        new ComponentPropertyDefinition("node") { Required = true },
    ];

    /// <inheritdoc />
    public ComponentSetup Setup(ComponentProperties properties, ComponentSetupContext context)
        => () =>
        {
            var node = properties.Get<CommentNode>("node");
            if (node is null)
            {
                return VirtualNodeFactory.Comment();
            }
            var comment = node.Comment;

            var children = new List<VirtualNode?>(3)
            {
                VirtualNodeFactory.Element(
                    "div",
                    VirtualNodeFactory.Properties(("class", "hn-comment-meta")),
                    Ui.Link($"/user/{comment.By}", "hn-by", comment.By ?? "unknown"),
                    Ui.Text("span", "hn-comment-time", $" · {Ui.TimeAgo(comment.Time)}")),
                Body(comment.Text),
            };

            var replies = Replies(node);
            if (replies is not null)
            {
                children.Add(replies);
            }

            return VirtualNodeFactory.Element(
                "li",
                VirtualNodeFactory.Properties(("class", "hn-comment")),
                children.ToArray());
        };

    private static VirtualNode Body(string? text)
    {
        var paragraphs = HtmlText.ToParagraphs(text);
        if (paragraphs.Count == 0)
        {
            return Ui.Text("div", "hn-comment-text hn-comment-empty", "[no text]");
        }
        var nodes = new VirtualNode?[paragraphs.Count];
        for (var index = 0; index < paragraphs.Count; index++)
        {
            nodes[index] = Ui.Text("p", null, paragraphs[index]);
        }
        return VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("class", "hn-comment-text")), nodes);
    }

    private static VirtualNode? Replies(CommentNode node)
    {
        if (node.Replies.Count == 0)
        {
            return node.HasMoreReplies
                ? Ui.Link($"/item/{node.Comment.Id}", "hn-more-replies", "more replies →")
                : null;
        }

        var items = new VirtualNode?[node.Replies.Count];
        for (var index = 0; index < node.Replies.Count; index++)
        {
            var reply = node.Replies[index];
            items[index] = VirtualNodeFactory.Component(
                Instance,
                VirtualNodeFactory.Properties(("key", reply.Comment.Id), ("node", reply)));
        }
        return VirtualNodeFactory.Element("ul", VirtualNodeFactory.Properties(("class", "hn-replies")), items);
    }
}
