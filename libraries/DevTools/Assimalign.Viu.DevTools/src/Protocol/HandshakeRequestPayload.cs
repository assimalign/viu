using System.Collections.Generic;

namespace Assimalign.Viu.DevTools;

/// <summary>The handshake request payload exchanged by the inspection protocol.</summary>
/// <remarks>Data-only contract; mutable collections are not thread-safe. Specified by <c>[DVT-2]</c>, <c>[DVT-13]</c>, and <c>[DVT-14]</c>.</remarks>
/// <param name="SupportedVersions">The versions accepted by the sender.</param>
public sealed record HandshakeRequestPayload(List<int> SupportedVersions);
