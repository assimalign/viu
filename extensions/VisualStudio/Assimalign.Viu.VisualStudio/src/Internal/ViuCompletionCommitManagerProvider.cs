using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Installs the Viu commit adapter for <c>.viu</c> buffers.
/// </summary>
[Export(typeof(IAsyncCompletionCommitManagerProvider))]
[Name(nameof(ViuCompletionCommitManagerProvider))]
[ContentType(ViuContentTypes.Viu)]
[TextViewRole(PredefinedTextViewRoles.Editable)]
internal sealed class ViuCompletionCommitManagerProvider : IAsyncCompletionCommitManagerProvider
{
    private static readonly object ManagerPropertyKey = new();

    /// <inheritdoc />
    public IAsyncCompletionCommitManager GetOrCreate(ITextView textView) =>
        textView.Properties.GetOrCreateSingletonProperty<IAsyncCompletionCommitManager>(
            ManagerPropertyKey,
            static () => new ViuCompletionCommitManager());
}
