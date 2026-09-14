using System.Collections.Generic;

namespace Assimalign.Viu.DevTools;

/// <summary>The component expansion response payload exchanged by the inspection protocol.</summary>
/// <remarks>Data-only contract; mutable collections are not thread-safe. Specified by <c>[DVT-2]</c>, <c>[DVT-13]</c>, and <c>[DVT-14]</c>.</remarks>
/// <param name="Identifier">The identity scoped to the connected runtime session.</param>
/// <param name="Section">The parameters or state snapshot section.</param>
/// <param name="Path">The ordered case-sensitive inspection path segments.</param>
/// <param name="Value">The wire value at this path.</param>
/// <param name="Error">The diagnostic message when data is unavailable.</param>
public sealed record ComponentExpansionResponsePayload(
    int Identifier,
    string Section,
    List<string> Path,
    DevToolsValuePayload? Value,
    string? Error);
