using System.Collections.Generic;

namespace Assimalign.Viu.DevTools;

/// <summary>The protocol batch exchanged by the inspection protocol.</summary>
/// <remarks>Data-only contract; mutable collections are not thread-safe. Specified by <c>[DVT-2]</c>, <c>[DVT-13]</c>, and <c>[DVT-14]</c>.</remarks>
/// <param name="Protocol">The protocol name.</param>
/// <param name="Messages">The envelopes in wire order.</param>
public sealed record ProtocolBatch(
    string Protocol,
    List<ProtocolEnvelope> Messages);
