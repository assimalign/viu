namespace Assimalign.Viu;

/// <summary>
/// Opaque evaluation-order token threaded by compiler-generated block and cache helper calls.
/// </summary>
/// <remarks>
/// The token exists because C# has no comma expression to sequence two calls inside one argument
/// list. Generated code passes the result of <see cref="RenderHelpers._openBlock"/> as the first
/// block-factory argument, which is what guarantees the block opens before its children are
/// evaluated — the ordering the whole block-collection protocol depends on
/// (<c>[RND-BLOCK-3]</c>). The runtime is single-threaded and the token carries no independently
/// useful state.
/// </remarks>
public readonly struct BlockToken
{
    internal BlockToken(int trackingDelta)
    {
        TrackingDelta = trackingDelta;
    }

    internal int TrackingDelta { get; }
}
