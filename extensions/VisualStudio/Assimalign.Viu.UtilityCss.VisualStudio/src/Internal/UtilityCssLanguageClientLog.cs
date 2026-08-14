using System.Diagnostics;

using Microsoft.VisualStudio.Shell;

namespace Assimalign.Viu.UtilityCss.VisualStudio;

/// <summary>Surfaces language-server diagnostic output through Visual Studio's activity log.</summary>
internal static class UtilityCssLanguageClientLog
{
    private const string SourceName = "Assimalign.Viu.UtilityCss.VisualStudio";

    /// <summary>Writes one standard-error line without allowing logging to disrupt the client.</summary>
    internal static void WriteServerStandardError(string message)
    {
        string entry = "Language server: " + message;
        if (!ActivityLog.TryLogInformation(SourceName, entry))
        {
            Trace.WriteLine(SourceName + ": " + entry);
        }
    }
}
