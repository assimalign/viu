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
        bool responseStarted)
    {
        Context = context;
        Failure = failure;
        ResponseStarted = responseStarted;
    }

    /// <summary>Gets whether rendering and request-scope disposal both completed successfully.</summary>
    public bool Succeeded => Failure is null;

    /// <summary>Gets the render, output, validation, or disposal failure, when one occurred.</summary>
    public Exception? Failure { get; }

    /// <summary>
    /// Gets whether the adaptor attempted to write any response content before completion or
    /// failure was known.
    /// </summary>
    /// <remarks>A host uses this signal to decide whether its status and headers can still change.</remarks>
    public bool ResponseStarted { get; }

    /// <summary>
    /// Gets the request-owned render context when scope creation reached that point, including
    /// resolved teleport output available after successful rendering.
    /// </summary>
    public SsrContext? Context { get; }
}
