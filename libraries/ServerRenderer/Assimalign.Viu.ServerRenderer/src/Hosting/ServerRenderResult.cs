using System;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Reports completion or failure to the downstream host without prescribing an HTTP status,
/// error page, or framework exception policy.
/// </summary>
/// <remarks>
/// Cancellation is not converted to a result and propagates as <see cref="OperationCanceledException"/>.
/// Specified by <c>[SSR-8]</c> and <c>[SSR-12]</c>.
/// </remarks>
public sealed class ServerRenderResult
{
    internal ServerRenderResult(
        SsrContext? context,
        Exception? failure,
        bool responseCommitted)
    {
        Context = context;
        Failure = failure;
        ResponseCommitted = responseCommitted;
    }

    /// <summary>Gets whether rendering and request-scope disposal both completed successfully.</summary>
    public bool Succeeded => Failure is null;

    /// <summary>Gets the render, output, validation, or disposal failure, when one occurred.</summary>
    public Exception? Failure { get; }

    /// <summary>
    /// Gets the host-reported response commitment state observed after rendering and scope
    /// teardown.
    /// </summary>
    /// <remarks>
    /// <see langword="false"/> guarantees that the host can still discard accepted content and
    /// send a clean replacement response. The value is copied from
    /// <see cref="IServerRenderOutput.ResponseCommitted"/> and is never inferred from attempted
    /// writes. Specified by <c>[SSR-12]</c>.
    /// </remarks>
    public bool ResponseCommitted { get; }

    /// <summary>
    /// Gets the request-owned render context when scope creation reached that point, including
    /// resolved teleport output available after successful rendering.
    /// </summary>
    public SsrContext? Context { get; }
}
