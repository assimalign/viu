namespace Assimalign.Viu;

/// <summary>
/// Opaque evaluation-order token threaded by compiler-generated block and cache helper calls.
/// </summary>
/// <remarks>
/// The token exists because C# has no JavaScript comma expression. Generated code passes the result
/// of <see cref="RenderHelpers._openBlock"/> as the first block-factory argument, preserving Vue's
/// open-before-children evaluation order. The runtime is single-threaded and the token carries no
/// independently useful state.
/// </remarks>
public readonly struct BlockToken
{
    internal BlockToken(int trackingDelta)
    {
        TrackingDelta = trackingDelta;
    }

    internal int TrackingDelta { get; }
}
