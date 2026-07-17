using System;

namespace Assimalign.Vue.RuntimeDom;

/// <summary>
/// A DOM interop failure surfaced as a typed exception carrying the node-op name and the handle
/// it targeted, instead of an opaque JS error string — the [V01.01.04.01] failure-semantics
/// contract for the bridge underneath <c>@vue/runtime-dom</c>'s <c>nodeOps</c>
/// (https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/nodeOps.ts).
/// </summary>
public sealed class BrowserDomException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="operationName">The bridge operation that failed (e.g. <c>"insert"</c>).</param>
    /// <param name="nodeHandle">The handle the operation targeted; 0 when handleless.</param>
    /// <param name="message">The failure description.</param>
    /// <param name="innerException">The underlying JS-interop exception, when any.</param>
    public BrowserDomException(string operationName, int nodeHandle, string message, Exception? innerException = null)
        : base($"DOM operation '{operationName}' failed for handle {nodeHandle}: {message}", innerException)
    {
        OperationName = operationName;
        NodeHandle = nodeHandle;
    }

    /// <summary>The bridge operation that failed (e.g. <c>"insert"</c>, <c>"setAttribute"</c>).</summary>
    public string OperationName { get; }

    /// <summary>The node handle the operation targeted; 0 when the operation had no handle.</summary>
    public int NodeHandle { get; }
}
