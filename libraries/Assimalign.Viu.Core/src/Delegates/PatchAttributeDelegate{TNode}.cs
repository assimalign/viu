namespace Assimalign.Viu;

/// <summary>
/// Applies one immutable component-attribute change to a platform element.
/// </summary>
/// <typeparam name="TNode">The platform node type.</typeparam>
/// <param name="element">The platform element.</param>
/// <param name="elementTag">The element tag already known by the renderer.</param>
/// <param name="attributeName">The attribute, property, or event-binding name.</param>
/// <param name="previousValue">The previous value, or null when mounting.</param>
/// <param name="nextValue">The next value, or null when removing.</param>
/// <param name="elementNamespace">The current platform namespace.</param>
public delegate void PatchAttributeDelegate<TNode>(
    TNode element,
    string elementTag,
    string attributeName,
    object? previousValue,
    object? nextValue,
    string? elementNamespace);
