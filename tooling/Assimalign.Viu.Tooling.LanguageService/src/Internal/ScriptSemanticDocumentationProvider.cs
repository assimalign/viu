using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Xml;

using Microsoft.CodeAnalysis;

namespace Assimalign.Viu.Tooling.LanguageService;

/// <summary>
/// A file-backed <see cref="DocumentationProvider"/> over a reference assembly's sibling
/// <c>.xml</c> documentation file ([V01.01.12.23], #259). The Roslyn implementation of this
/// extension point (<c>XmlDocumentationProvider</c>) ships in the Workspaces assembly the
/// SemanticModel-only backend deliberately excludes, so the engine carries this minimal
/// equivalent: the file is parsed lazily, once, into a member-id → element-XML map with a plain
/// <see cref="XmlReader"/> — no serialization, no reflection, trimming-safe.
/// </summary>
internal sealed class ScriptSemanticDocumentationProvider : DocumentationProvider
{
    private readonly string documentationPath;
    private readonly object synchronization = new();
    private Dictionary<string, string>? membersByIdentifier;

    internal ScriptSemanticDocumentationProvider(string documentationPath)
    {
        this.documentationPath = documentationPath;
    }

    /// <inheritdoc/>
    protected override string GetDocumentationForSymbol(
        string documentationMemberID,
        CultureInfo preferredCulture,
        CancellationToken cancellationToken = default)
    {
        var members = GetMembers();
        return members.TryGetValue(documentationMemberID, out var documentation)
            ? documentation
            : string.Empty;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is ScriptSemanticDocumentationProvider other &&
           string.Equals(
               documentationPath,
               other.documentationPath,
               StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public override int GetHashCode()
        => StringComparer.OrdinalIgnoreCase.GetHashCode(documentationPath);

    private Dictionary<string, string> GetMembers()
    {
        lock (synchronization)
        {
            return membersByIdentifier ??= ReadMembers();
        }
    }

    // One pass over `<member name="...">` elements; each member's outer XML is stored whole so the
    // consumer sees the same shape the compiler's own /doc reader produces. Any read or parse
    // failure degrades to no documentation rather than a request failure.
    private Dictionary<string, string> ReadMembers()
    {
        var members = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            using var stream = File.OpenRead(documentationPath);
            using var reader = XmlReader.Create(
                stream,
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    IgnoreComments = true,
                    IgnoreProcessingInstructions = true,
                });
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element ||
                    !string.Equals(reader.Name, "member", StringComparison.Ordinal))
                {
                    continue;
                }

                var identifier = reader.GetAttribute("name");
                if (string.IsNullOrEmpty(identifier))
                {
                    continue;
                }

                members[identifier] = reader.ReadOuterXml();
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            members.Clear();
        }

        return members;
    }
}
