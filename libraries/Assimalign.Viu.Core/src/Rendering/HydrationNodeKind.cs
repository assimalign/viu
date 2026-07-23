namespace Assimalign.Viu;

/// <summary>
/// Identifies an existing host node while a server-rendered tree is being hydrated.
/// </summary>
/// <remarks>
/// This is the host-neutral equivalent of the DOM node-type checks used by Vue 3.5's
/// hydration walker:
/// https://github.com/vuejs/core/blob/v3.5.29/packages/runtime-core/src/hydration.ts.
/// </remarks>
public enum HydrationNodeKind
{
    /// <summary>An element node.</summary>
    Element,

    /// <summary>A text node.</summary>
    Text,

    /// <summary>A comment node.</summary>
    Comment,

    /// <summary>A host node that cannot be adopted by the component renderer.</summary>
    Other,
}
