using System.Threading;

namespace Assimalign.Viu.LanguageServer;

/// <summary>Identifies one in-flight request's document and cancellation source.</summary>
internal sealed record LanguageServerPendingRequestCancellation(
    string DocumentUri,
    CancellationTokenSource Source);
