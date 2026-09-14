using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Reads the shortcut catalog from the shipped Visual Studio snippet files. Specified by
/// <c>[V01.01.12.29]</c> (#344).
/// </summary>
/// <remarks>
/// The files are the list: adding a snippet under the registered Snippets directory needs no code
/// entry. This reader touches no editor types; the handler supplies the activity-log sink and keeps
/// the result for its lifetime so no file is read on a keystroke. A failed load disables the entire
/// catalog, including any shortcuts read before the failure, rather than publishing a partial set.
/// </remarks>
internal static class ViuSnippetCatalog
{
    private const string SnippetNamespace =
        "http://schemas.microsoft.com/VisualStudio/2005/CodeSnippet";

    /// <summary>
    /// Reads every snippet below the directory into one ordinal set, or logs a failure and returns
    /// an empty set. Streams are opened and disposed here, including when parsing fails.
    /// </summary>
    /// <param name="directory">The installed folder named by the pkgdef CodeExpansions Paths entry.</param>
    /// <param name="logFailure">Receives one diagnostic naming the failing file or directory.</param>
    /// <param name="enumerateFiles">Optional file enumeration for editor-free failure tests.</param>
    /// <param name="openRead">Optional stream source for editor-free failure tests.</param>
    /// <returns>An ordinal set that the caller retains without mutation.</returns>
    internal static ISet<string> Read(
        string directory,
        Action<string> logFailure,
        Func<string, IEnumerable<string>>? enumerateFiles = null,
        Func<string, Stream>? openRead = null)
    {
        var shortcuts = new HashSet<string>(StringComparer.Ordinal);
        string source = directory;
        try
        {
            enumerateFiles ??= path => Directory.EnumerateFiles(
                path, "*.snippet", SearchOption.AllDirectories);
            openRead ??= File.OpenRead;

            foreach (string path in enumerateFiles(directory))
            {
                source = path;
                using (Stream stream = openRead(path))
                using (XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                }))
                {
                    var document = new XmlDocument { XmlResolver = null };
                    document.Load(reader);
                    var namespaces = new XmlNamespaceManager(document.NameTable);
                    namespaces.AddNamespace("snippet", SnippetNamespace);
                    XmlNodeList snippets = document.SelectNodes(
                        "/snippet:CodeSnippets/snippet:CodeSnippet", namespaces)!;
                    if (snippets.Count == 0)
                    {
                        throw new InvalidDataException("No CodeSnippet/Header/Shortcut was found.");
                    }

                    foreach (XmlNode snippet in snippets)
                    {
                        string? shortcut = snippet.SelectSingleNode(
                            "snippet:Header/snippet:Shortcut", namespaces)?.InnerText;
                        if (shortcut is null || string.IsNullOrWhiteSpace(shortcut))
                        {
                            throw new InvalidDataException("A CodeSnippet has no Header/Shortcut value.");
                        }

                        shortcuts.Add(shortcut);
                    }
                }

                // A deferred enumeration failure belongs to the directory, not the preceding file.
                source = directory;
            }
        }
        catch (Exception exception)
        {
            // Loading optional editor data must never break handler composition or take a keystroke.
            shortcuts.Clear();
            try
            {
                logFailure($"Snippet shortcuts are disabled: could not read '{source}'. {exception.Message}");
            }
            catch (Exception)
            {
                // An unavailable activity log must not turn a disabled catalog into an editor fault.
            }
        }

        return shortcuts;
    }
}
