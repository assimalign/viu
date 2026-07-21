using System;
using System.Diagnostics;

namespace Assimalign.Viu;

/// <summary>
/// The dev-warning seam (upstream: <c>warn()</c> in <c>@vue/runtime-core</c>): props
/// validation, emit validation, and lifecycle misuse report here. Defaults to a debug trace;
/// tests capture it, and the app-level configuration ([V01.01.03.12]) will route it.
/// </summary>
internal static class RuntimeWarnings
{
    /// <summary>The warning sink; never null.</summary>
    internal static Action<string> Sink { get; set; } = static message => Debug.WriteLine($"[Vue warn] {message}");

    /// <summary>Emits a dev warning.</summary>
    /// <param name="message">The warning text.</param>
    internal static void Warn(string message) => Sink(message);
}
