using System.Threading;

using StreamJsonRpc;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Holds the connection to the running Viu language server for the features that ask it questions
/// Visual Studio does not ask on their behalf.
/// </summary>
/// <remarks>
/// The Quick Info adapter is one such feature: it renders the tooltip itself, so it needs the hover
/// answer rather than Visual Studio's rendering of it. The connection is process-wide because the
/// client is, and it is cleared when the client stops so a stale channel is never used against a
/// server that has gone.
/// </remarks>
internal static class ViuLanguageServerConnection
{
    private static JsonRpc? current;

    /// <summary>Gets the live connection, or <see langword="null"/> when no server is attached.</summary>
    internal static JsonRpc? Current => Volatile.Read(ref current);

    /// <summary>Records the connection Visual Studio attached for custom messages.</summary>
    /// <param name="rpc">The connection, or <see langword="null"/> to clear it.</param>
    internal static void Attach(JsonRpc? rpc) => Volatile.Write(ref current, rpc);
}
