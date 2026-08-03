namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// The comment-marker vocabulary the SSR output embeds so the client hydration walker
/// ([V01.01.07.03]) can align the server DOM with the client component tree. These byte sequences
/// are a cross-package contract: the walker matches on them exactly, so they are centralized here
/// rather than inlined, and changing one is a breaking change to the hydration protocol — markup
/// already served by a deployed application would no longer hydrate. Specified by
/// <c>[SSR-MARKERS-1]</c> and <c>[SSR-MARKERS-2]</c>.
/// </summary>
internal static class SsrMarkers
{
    /// <summary>
    /// Opens a fragment's children. A fragment component — a multi-root component, a <c>v-for</c>
    /// block, a slot outlet — brackets its children with
    /// <see cref="FragmentStart"/>/<see cref="FragmentEnd"/> so the hydration walker knows the fragment's
    /// child range without an element wrapper.
    /// </summary>
    public const string FragmentStart = "<!--[-->";

    /// <summary>Closes a fragment's children opened by <see cref="FragmentStart"/>.</summary>
    public const string FragmentEnd = "<!--]-->";

    /// <summary>
    /// An empty comment node / comment anchor. Emitted for a comment component with empty content
    /// and as the <c>v-if</c>-false placeholder, giving hydration a stable anchor node to adopt.
    /// </summary>
    public const string EmptyComment = "<!---->";

    /// <summary>
    /// Marks a <c>&lt;Teleport&gt;</c>'s position in the main document. The teleported content itself
    /// is buffered against the target selector (see <see cref="SsrContext.Teleports"/>); only this
    /// start/end anchor pair marks the origin.
    /// </summary>
    public const string TeleportStart = "<!--teleport start-->";

    /// <summary>Closes a <c>&lt;Teleport&gt;</c>'s main-document position.</summary>
    public const string TeleportEnd = "<!--teleport end-->";

    /// <summary>
    /// Terminates the teleported content inside the target buffer, emitted after the children. The
    /// hydration walker uses it to bound the adopted target-side range, so a host that splices a
    /// target buffer into its element must keep this trailing marker intact.
    /// </summary>
    public const string TeleportAnchor = "<!--teleport anchor-->";
}
