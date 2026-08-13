using System;

namespace Assimalign.Viu.Router;

/// <summary>
/// An immutable scroll destination selected by <see cref="ScrollBehavior"/>: either absolute
/// document coordinates or a selector with an offset, with optional smooth motion.
/// </summary>
/// <remarks>
/// Selectors are resolved only by a browser integration after the post-render flush. The host-free
/// router carries this value without inspecting a document. Specified by <c>[V01.01.08.05]</c>.
/// </remarks>
public sealed class ScrollTarget
{
    /// <summary>Creates an absolute document-coordinate target.</summary>
    /// <param name="position">The absolute horizontal and vertical offset.</param>
    public ScrollTarget(ScrollPosition position)
        : this(position, smooth: false)
    {
    }

    /// <summary>Creates an absolute document-coordinate target.</summary>
    /// <param name="position">The absolute horizontal and vertical offset.</param>
    /// <param name="smooth">Whether to request smooth motion.</param>
    public ScrollTarget(ScrollPosition position, bool smooth)
    {
        Position = position;
        Smooth = smooth;
    }

    /// <summary>Creates a target relative to the first element matching a CSS selector.</summary>
    /// <param name="selector">The non-empty selector resolved after rendering.</param>
    /// <exception cref="ArgumentException"><paramref name="selector"/> is null or empty.</exception>
    public ScrollTarget(string selector)
        : this(selector, default, smooth: false)
    {
    }

    /// <summary>Creates a target relative to the first element matching a CSS selector.</summary>
    /// <param name="selector">The non-empty selector resolved after rendering.</param>
    /// <param name="offset">The amount subtracted from the element's document coordinates.</param>
    /// <param name="smooth">Whether to request smooth motion.</param>
    /// <exception cref="ArgumentException"><paramref name="selector"/> is null or empty.</exception>
    public ScrollTarget(
        string selector,
        ScrollPosition offset,
        bool smooth)
    {
        ArgumentException.ThrowIfNullOrEmpty(selector);
        Selector = selector;
        Offset = offset;
        Smooth = smooth;
    }

    /// <summary>
    /// Gets the absolute coordinates, or <see langword="null"/> when <see cref="Selector"/> names
    /// the target element.
    /// </summary>
    public ScrollPosition? Position { get; }

    /// <summary>
    /// Gets the selector resolved by the browser host, or <see langword="null"/> for absolute
    /// coordinates.
    /// </summary>
    public string? Selector { get; }

    /// <summary>Gets the offset subtracted from selector-derived document coordinates.</summary>
    public ScrollPosition Offset { get; }

    /// <summary>Gets whether the browser should animate the scroll smoothly.</summary>
    public bool Smooth { get; }
}
