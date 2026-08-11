using System;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Reads an editor document's current path without freezing its pre-Save-As identity.
/// </summary>
internal sealed class ViuCompletionDocumentPath
{
    private readonly Func<string> getCurrentPath;

    /// <summary>Initializes a path reader over a live editor document.</summary>
    internal ViuCompletionDocumentPath(Func<string> getCurrentPath) =>
        this.getCurrentPath = getCurrentPath ??
            throw new ArgumentNullException(nameof(getCurrentPath));

    /// <summary>Gets the document path at the time the completion list is presented.</summary>
    internal string Current => this.getCurrentPath();
}
