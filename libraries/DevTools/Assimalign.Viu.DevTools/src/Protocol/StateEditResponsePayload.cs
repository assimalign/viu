using System.Collections.Generic;

namespace Assimalign.Viu.DevTools;

/// <summary>The state edit response payload exchanged by the inspection protocol.</summary>
/// <remarks>Data-only contract; mutable collections are not thread-safe. Specified by <c>[DVT-2]</c>, <c>[DVT-13]</c>, and <c>[DVT-14]</c>.</remarks>
/// <param name="Identifier">The identity scoped to the connected runtime session.</param>
/// <param name="Path">The ordered case-sensitive inspection path segments.</param>
/// <param name="Accepted">Whether negotiation or the write succeeded.</param>
/// <param name="Reason">The diagnostic rejection reason, omitted on success.</param>
public sealed record StateEditResponsePayload(int Identifier, List<string> Path, bool Accepted, string? Reason);
