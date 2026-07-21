namespace Assimalign.Viu.RuntimeCore;

/// <summary>
/// Classifies a server/client hydration mismatch for dev-mode reporting and the
/// <c>data-allow-mismatch</c> escape hatch — the C# port of the <c>MismatchTypes</c> enum in
/// <c>@vue/runtime-core</c>'s hydration module (<c>packages/runtime-core/src/hydration.ts</c>,
/// https://vuejs.org/guide/scaling-up/ssr.html#hydration-mismatch). The string forms
/// (<c>"text"</c>, <c>"children"</c>, <c>"class"</c>, <c>"style"</c>, <c>"attribute"</c>) are the
/// tokens a <c>data-allow-mismatch="..."</c> attribute lists to suppress a given kind.
/// </summary>
internal enum HydrationMismatchType
{
    /// <summary>A text node's content differs (upstream: <c>MismatchTypes.TEXT</c>).</summary>
    Text,

    /// <summary>An element's child set differs — too many or too few nodes, or a node-type mismatch (upstream: <c>CHILDREN</c>).</summary>
    Children,

    /// <summary>A <c>class</c> binding differs (upstream: <c>CLASS</c>).</summary>
    Class,

    /// <summary>A <c>style</c> binding differs (upstream: <c>STYLE</c>).</summary>
    Style,

    /// <summary>A plain attribute differs (upstream: <c>ATTRIBUTE</c>).</summary>
    Attribute,
}
