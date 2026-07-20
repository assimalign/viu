using System;
using System.Collections.Generic;

using Assimalign.Viu.Router;
using Assimalign.Viu.RuntimeCore;

namespace Assimalign.Viu.HackerNews;

/// <summary>
/// Small render-function helpers shared by the views. Two things matter here:
/// <list type="bullet">
/// <item>The <see cref="RouterLink"/>/<see cref="RouterView"/> definitions are <b>shared singletons</b>.
/// A component vnode's identity is its definition instance, so reusing one instance everywhere (exactly
/// how Vue treats a globally-registered component) lets the renderer patch links/outlets in place across
/// re-renders instead of unmount/remounting them; keys distinguish the per-position instances.</item>
/// <item>Every node is built through <see cref="VirtualNodeFactory"/> only — no DOM or interop touched
/// during render, so the views stay SSR-ready and renderer-agnostic (#103).</item>
/// </list>
/// </summary>
internal static class Ui
{
    /// <summary>The shared <see cref="Router.RouterLink"/> definition used for every in-app link.</summary>
    public static readonly RouterLink RouterLink = new();

    /// <summary>The shared <see cref="Router.RouterView"/> outlet definition.</summary>
    public static readonly RouterView RouterView = new();

    /// <summary>An element with an optional class and child vnodes.</summary>
    public static VirtualNode Element(string tag, string? cssClass, params VirtualNode?[] children)
        => VirtualNodeFactory.Element(
            tag,
            cssClass is null ? null : VirtualNodeFactory.Properties(("class", cssClass)),
            children);

    /// <summary>An element with an optional class and text content.</summary>
    public static VirtualNode Text(string tag, string? cssClass, string content)
        => VirtualNodeFactory.Element(
            tag,
            cssClass is null ? null : VirtualNodeFactory.Properties(("class", cssClass)),
            content);

    /// <summary>A raw text vnode.</summary>
    public static VirtualNode Raw(string content) => VirtualNodeFactory.Text(content);

    /// <summary>An in-app <see cref="Router.RouterLink"/> with a text label (client-side navigation).</summary>
    public static VirtualNode Link(string to, string? cssClass, string label)
        => Link(to, cssClass, VirtualNodeFactory.Text(label));

    /// <summary>An in-app <see cref="Router.RouterLink"/> whose label is arbitrary child vnodes.</summary>
    public static VirtualNode Link(string to, string? cssClass, params VirtualNode?[] label)
    {
        var slots = new ComponentSlots();
        slots["default"] = _ => label;
        var properties = cssClass is null
            ? VirtualNodeFactory.Properties(("to", to))
            : VirtualNodeFactory.Properties(("to", to), ("class", cssClass));
        return VirtualNodeFactory.Component(RouterLink, properties, slots);
    }

    /// <summary>An external anchor (story targets) that opens in a new, isolated tab.</summary>
    public static VirtualNode ExternalLink(string href, string? cssClass, string label)
        => VirtualNodeFactory.Element(
            "a",
            cssClass is null
                ? VirtualNodeFactory.Properties(("href", href), ("target", "_blank"), ("rel", "noopener noreferrer"))
                : VirtualNodeFactory.Properties(("href", href), ("target", "_blank"), ("rel", "noopener noreferrer"), ("class", cssClass)),
            label);

    /// <summary>A coarse "N units ago" label (Vue-hn parity display). Uses wall-clock time.</summary>
    public static string TimeAgo(DateTimeOffset time)
    {
        var delta = DateTimeOffset.UtcNow - time;
        if (delta < TimeSpan.Zero)
        {
            delta = TimeSpan.Zero;
        }
        if (delta.TotalMinutes < 1)
        {
            return "just now";
        }
        if (delta.TotalMinutes < 60)
        {
            return Plural((int)delta.TotalMinutes, "minute") + " ago";
        }
        if (delta.TotalHours < 24)
        {
            return Plural((int)delta.TotalHours, "hour") + " ago";
        }
        return Plural((int)delta.TotalDays, "day") + " ago";
    }

    /// <summary>Formats a count with a singular/plural noun, e.g. <c>1 comment</c> / <c>5 comments</c>.</summary>
    public static string Plural(int count, string singular)
        => count == 1 ? $"1 {singular}" : $"{count} {singular}s";

    /// <summary>Builds the route path for a feed page (page 1 omits the trailing segment for a clean URL).</summary>
    public static string FeedPath(StoryFeed feed, int page)
        => page <= 1 ? $"/{StoryFeeds.ToSlug(feed)}" : $"/{StoryFeeds.ToSlug(feed)}/{page}";
}
