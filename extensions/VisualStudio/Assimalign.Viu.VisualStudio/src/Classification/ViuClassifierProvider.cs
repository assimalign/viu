using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Supplies the lexical-plus-semantic classifier for buffers carrying the <c>viu</c> content type.
/// </summary>
/// <remarks>
/// One classifier per buffer, held in the buffer's own property collection so it lives and dies with
/// the buffer and every view over the same document shares one lexer result.
/// </remarks>
[Export(typeof(IClassifierProvider))]
[ContentType(ViuContentTypes.Viu)]
internal sealed class ViuClassifierProvider : IClassifierProvider
{
    private readonly IClassificationTypeRegistryService classificationTypeRegistry;
    private readonly ITextDocumentFactoryService textDocumentFactory;

    /// <summary>
    /// Initializes the provider with the registry the classifier resolves classification types from.
    /// </summary>
    /// <param name="classificationTypeRegistry">The editor's classification type registry.</param>
    /// <param name="textDocumentFactory">
    /// The document service used only to associate the buffer with the URI published by the server.
    /// </param>
    [ImportingConstructor]
    public ViuClassifierProvider(
        IClassificationTypeRegistryService classificationTypeRegistry,
        ITextDocumentFactoryService textDocumentFactory)
    {
        this.classificationTypeRegistry = classificationTypeRegistry;
        this.textDocumentFactory = textDocumentFactory;
    }

    /// <inheritdoc />
    public IClassifier GetClassifier(ITextBuffer textBuffer) =>
        textBuffer.Properties.GetOrCreateSingletonProperty(
            () => new ViuClassifier(
                textBuffer,
                this.classificationTypeRegistry,
                ViuSemanticClassificationState.Shared,
                this.textDocumentFactory));
}
