namespace Assimalign.Viu.RuntimeCore;

/// <summary>
/// The comment-marker vocabulary the hydration walker matches against the server-rendered DOM —
/// the C# port of the literal marker strings <c>@vue/runtime-core</c>'s hydration module tests for
/// (<c>packages/runtime-core/src/hydration.ts</c>: <c>node.data === '['</c>, <c>=== ']'</c>,
/// <c>=== 'teleport start'</c>, and so on; https://vuejs.org/guide/scaling-up/ssr.html#client-hydration).
/// These values are the <b>content</b> of the comment nodes (what
/// <see cref="HydrationNodeReader{TNode}.Data"/> returns — the text between <c>&lt;!--</c> and
/// <c>--&gt;</c>), so <see cref="FragmentStart"/> is <c>[</c>, not <c>&lt;!--[--&gt;</c>.
/// <para>
/// This is a cross-package <b>output convention</b>, not shared code: the SSR renderer
/// (<c>Assimalign.Viu.ServerRenderer</c>'s <c>SsrMarkers</c>) emits the same byte sequences, and
/// hydration adopts them. The two ends are deliberately decoupled — hydration must not take a code
/// dependency on the server renderer (issue #66 architectural boundary) — so the constants are
/// duplicated here and pinned to the same upstream reference; changing one without the other breaks
/// the hydration protocol.
/// </para>
/// </summary>
internal static class HydrationMarkers
{
    /// <summary>
    /// The content of a fragment's opening comment (upstream: <c>node.data === '['</c>; the SSR side
    /// emits <c>&lt;!--[--&gt;</c>). Brackets a fragment vnode's children — a multi-root component, a
    /// <c>v-for</c> block, a slot outlet — with no element wrapper.
    /// </summary>
    public const string FragmentStart = "[";

    /// <summary>The content of a fragment's closing comment (upstream: <c>node.data === ']'</c>; SSR emits <c>&lt;!--]--&gt;</c>).</summary>
    public const string FragmentEnd = "]";

    /// <summary>
    /// The content of a teleport's main-tree start anchor (upstream: <c>node.data === 'teleport start'</c>;
    /// SSR emits <c>&lt;!--teleport start--&gt;</c>). Marks the teleport's origin position; its content lives
    /// at the resolved target (enabled) or between this and <see cref="TeleportEnd"/> (disabled).
    /// </summary>
    public const string TeleportStart = "teleport start";

    /// <summary>The content of a teleport's main-tree end anchor (upstream: <c>'teleport end'</c>; SSR emits <c>&lt;!--teleport end--&gt;</c>).</summary>
    public const string TeleportEnd = "teleport end";

    /// <summary>
    /// The content of the anchor terminating a teleport's content inside its target (upstream:
    /// <c>'teleport anchor'</c>; SSR emits <c>&lt;!--teleport anchor--&gt;</c>). Bounds the target-side range
    /// the walker adopts.
    /// </summary>
    public const string TeleportAnchor = "teleport anchor";
}
