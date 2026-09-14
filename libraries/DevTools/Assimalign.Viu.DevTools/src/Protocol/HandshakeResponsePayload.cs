namespace Assimalign.Viu.DevTools;

/// <summary>The handshake response payload exchanged by the inspection protocol.</summary>
/// <remarks>Data-only contract; mutable collections are not thread-safe. Specified by <c>[DVT-2]</c>, <c>[DVT-13]</c>, and <c>[DVT-14]</c>.</remarks>
/// <param name="Accepted">Whether negotiation or the write succeeded.</param>
/// <param name="Version">The protocol or recorded dependency version.</param>
/// <param name="Reason">The diagnostic rejection reason, omitted on success.</param>
public sealed record HandshakeResponsePayload(
    bool Accepted,
    int? Version,
    string? Reason);
