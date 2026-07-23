using System;
using System.Diagnostics;

namespace Assimalign.Viu.Reactivity;

/// <summary>Internal development-warning seam for read-only reactive writes.</summary>
internal static class RuntimeWarnings
{
    internal static Action<string> Sink { get; set; }
        = static message => Debug.WriteLine($"[Vue warn] {message}");

    internal static void Warn(string message)
        => Sink(message);
}
