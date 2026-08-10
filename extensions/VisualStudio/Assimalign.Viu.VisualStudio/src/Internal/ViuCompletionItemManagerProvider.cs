using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Installs the Viu swatch wrapper ahead of Visual Studio's default Async Completion item manager.
/// </summary>
[Export(typeof(IAsyncCompletionItemManagerProvider))]
[Name(nameof(ViuCompletionItemManagerProvider))]
[ContentType(ViuContentTypes.Viu)]
[TextViewRole(PredefinedTextViewRoles.Editable)]
[Order(Before = PredefinedCompletionNames.DefaultCompletionItemManager)]
internal sealed class ViuCompletionItemManagerProvider : IAsyncCompletionItemManagerProvider
{
    private static readonly object ManagerPropertyKey = new();

    private readonly ITextDocumentFactoryService documentFactory;
    private readonly Lazy<IAsyncCompletionItemManagerProvider, IOrderable> defaultProvider;
    private readonly ViuCompletionColorImageCatalog imageCatalog;

    /// <summary>Initializes the provider from editor and shell services supplied by Visual Studio.</summary>
    [ImportingConstructor]
    internal ViuCompletionItemManagerProvider(
        ITextDocumentFactoryService documentFactory,
        [Import(typeof(SVsServiceProvider))]
        IServiceProvider serviceProvider,
        [ImportMany]
        IEnumerable<Lazy<IAsyncCompletionItemManagerProvider, IOrderable>> providers)
    {
        this.documentFactory = documentFactory ??
            throw new ArgumentNullException(nameof(documentFactory));
        this.defaultProvider = providers?.FirstOrDefault(provider =>
                string.Equals(
                    provider.Metadata.Name,
                    PredefinedCompletionNames.DefaultCompletionItemManager,
                    StringComparison.Ordinal)) ??
            throw new InvalidOperationException(
                "Visual Studio's default Async Completion item manager is unavailable.");

        var imageService = serviceProvider?.GetService(typeof(SVsImageService)) as
            IVsManagedImageService;
        this.imageCatalog = new ViuCompletionColorImageCatalog(imageService);
    }

    /// <inheritdoc />
    public IAsyncCompletionItemManager GetOrCreate(ITextView textView)
    {
        if (textView is null)
        {
            throw new ArgumentNullException(nameof(textView));
        }

        return textView.Properties.GetOrCreateSingletonProperty<IAsyncCompletionItemManager>(
            ManagerPropertyKey,
            () =>
            {
                IAsyncCompletionItemManager inner =
                    this.defaultProvider.Value.GetOrCreate(textView);
                if (!this.documentFactory.TryGetTextDocument(
                        textView.TextBuffer,
                        out ITextDocument document))
                {
                    return inner;
                }

                return new ViuCompletionItemManager(
                    inner,
                    new ViuCompletionDocumentPath(() => document.FilePath),
                    ViuCompletionColorState.Shared,
                    this.imageCatalog);
            });
    }
}
