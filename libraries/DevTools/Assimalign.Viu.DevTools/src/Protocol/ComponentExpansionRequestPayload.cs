using System.Collections.Generic;

namespace Assimalign.Viu.DevTools;

/// <summary>The component expansion request payload exchanged by the inspection protocol.</summary>
/// <remarks>Data-only contract; mutable collections are not thread-safe. Specified by <c>[DVT-2]</c>, <c>[DVT-13]</c>, and <c>[DVT-14]</c>.</remarks>
/// <param name="Identifier">The identity scoped to the connected runtime session.</param>
/// <param name="Section">The parameters or state snapshot section.</param>
/// <param name="Path">The ordered case-sensitive inspection path segments.</param>
/// <param name="Depth">The requested expansion depth, clamped by the runtime.</param>
public sealed record ComponentExpansionRequestPayload(
    int Identifier,
    string Section,
    List<string> Path,
    int Depth);
