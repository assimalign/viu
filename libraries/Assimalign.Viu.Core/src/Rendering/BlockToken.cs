namespace Assimalign.Viu;

/// <summary>
/// The opaque evaluation-order token the compiled render threads from <see cref="RenderHelpers._openBlock"/>
/// and <see cref="RenderHelpers._setBlockTracking"/> into the block factories and
/// <see cref="RenderHelpers._setCache"/>. Upstream Vue emits a JavaScript comma sequence —
/// <c>(openBlock(), createElementBlock(...))</c> — to open the block before any child argument is built;
/// C# has no comma operator, so the emitter threads this token as the factory's first argument instead
/// (see <c>Assimalign.Viu.Syntax.Templates/docs/DESIGN.md</c>). Because C# guarantees left-to-right
/// argument evaluation, passing the token runs <c>_openBlock()</c>/<c>_setBlockTracking()</c> before any
/// sibling argument is evaluated — reproducing upstream's open/collect/close (and pause/create/resume)
/// ordering. The token value is meaningless to the block factories; <see cref="RenderHelpers._setCache"/>
/// alone reads <see cref="TrackingDelta"/> to undo the tracking a preceding <c>_setBlockTracking</c>
/// applied. Not a stable public payload — it is an opaque contract handle.
/// </summary>
public readonly struct BlockToken
{
    /// <summary>Creates a token recording the tracking delta a <c>_setBlockTracking</c> call applied.</summary>
    /// <param name="trackingDelta">The signed amount passed to <c>_setBlockTracking</c> (0 for <c>_openBlock</c>).</param>
    internal BlockToken(int trackingDelta) => TrackingDelta = trackingDelta;

    /// <summary>The tracking delta a <c>_setBlockTracking</c> applied, so <c>_setCache</c> can invert it.</summary>
    internal int TrackingDelta { get; }
}
