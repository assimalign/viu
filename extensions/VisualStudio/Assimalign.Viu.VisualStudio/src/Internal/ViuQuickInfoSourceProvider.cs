using System;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Installs the Viu tooltip adapter for <c>.viu</c> buffers.
/// </summary>
[Export(typeof(IAsyncQuickInfoSourceProvider))]
[Name(nameof(ViuQuickInfoSourceProvider))]
[ContentType(ViuContentTypes.Viu)]
internal sealed class ViuQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
{
    private static readonly object SourcePropertyKey = new();

    private readonly ITextDocumentFactoryService documentFactory;

    /// <summary>Initializes the provider from the editor services Visual Studio supplies.</summary>
    [ImportingConstructor]
    internal ViuQuickInfoSourceProvider(ITextDocumentFactoryService documentFactory) =>
        this.documentFactory = documentFactory ??
            throw new ArgumentNullException(nameof(documentFactory));

    /// <inheritdoc />
    public IAsyncQuickInfoSource? TryCreateQuickInfoSource(ITextBuffer textBuffer)
    {
        if (textBuffer is null ||
            !this.documentFactory.TryGetTextDocument(textBuffer, out ITextDocument document))
        {
            return null;
        }

        return textBuffer.Properties.GetOrCreateSingletonProperty<IAsyncQuickInfoSource>(
            SourcePropertyKey,
            () => new ViuQuickInfoSource(
                textBuffer,
                new ViuCompletionDocumentPath(() => document.FilePath)));
    }
}
