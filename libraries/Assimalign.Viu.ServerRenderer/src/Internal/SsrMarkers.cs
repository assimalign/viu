namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// The comment-marker vocabulary the SSR output embeds so the client hydration walker
/// ([V01.01.07.03]) can align the server DOM with the client component tree — the C# port of the
/// literal marker strings in <c>@vue/server-renderer</c> (<c>packages/server-renderer/src/render.ts</c>
/// and <c>helpers/ssrRenderTeleport.ts</c>) and the hydration counterparts in
/// <c>@vue/runtime-core</c> (<c>packages/runtime-core/src/hydration.ts</c>). These byte sequences are
/// a cross-package contract: the hydration walker matches on them exactly, so they are centralized
/// here rather than inlined, and changing one is a breaking change to the hydration protocol.
/// See https://vuejs.org/guide/scaling-up/ssr.html#client-hydration.
/// </summary>
internal static class SsrMarkers
{
    /// <summary>
    /// Opens a fragment's children (upstream: <c>&lt;!--[--&gt;</c>). A fragment component — a multi-root
    /// component, a <c>v-for</c> block, a slot outlet — brackets its children with
    /// <see cref="FragmentStart"/>/<see cref="FragmentEnd"/> so the hydration walker knows the fragment's
    /// child range without an element wrapper.
    /// </summary>
    public const string FragmentStart = "<!--[-->";

    /// <summary>Closes a fragment's children (upstream: <c>&lt;!--]--&gt;</c>).</summary>
    public const string FragmentEnd = "<!--]-->";

    /// <summary>
    /// An empty comment node / comment anchor (upstream: <c>&lt;!----&gt;</c>). Emitted for a comment
    /// component with empty content and as the <c>v-if</c>-false placeholder, giving hydration a stable
    /// anchor node to adopt.
    /// </summary>
    public const string EmptyComment = "<!---->";

    /// <summary>
    /// Marks a <c>&lt;Teleport&gt;</c>'s position in the main document (upstream: the
    /// <c>&lt;!--teleport start--&gt;</c> pushed by <c>ssrRenderTeleport</c>). The teleported content
    /// itself is buffered against the target selector (see
    /// <see cref="SsrContext.Teleports"/>); only this start/end anchor pair marks the origin.
    /// </summary>
    public const string TeleportStart = "<!--teleport start-->";

    /// <summary>Closes a <c>&lt;Teleport&gt;</c>'s main-document position (upstream: <c>&lt;!--teleport end--&gt;</c>).</summary>
    public const string TeleportEnd = "<!--teleport end-->";

    /// <summary>
    /// Terminates the teleported content inside the target buffer (upstream: the
    /// <c>&lt;!--teleport anchor--&gt;</c> pushed after the children in <c>ssrRenderTeleport</c>). The
    /// hydration walker uses it to bound the adopted target-side range.
    /// </summary>
    public const string TeleportAnchor = "<!--teleport anchor-->";
}
