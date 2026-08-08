namespace Assimalign.Viu.Browser;

/// <summary>Applies one finalized browser command frame and reports released node handles.</summary>
/// <param name="frame">The reusable backing array containing the frame.</param>
/// <param name="length">The number of valid bytes in <paramref name="frame"/>.</param>
/// <returns>The handles released while applying the frame.</returns>
/// <remarks>
/// The callback is synchronous and must not retain or mutate the reusable array. One invocation is
/// one host interop crossing. Specified by <c>[RND-IO-1]</c> and <c>[EXE-13]</c>.
/// </remarks>
public delegate int[] BrowserCommandFrameApplier(byte[] frame, int length);
