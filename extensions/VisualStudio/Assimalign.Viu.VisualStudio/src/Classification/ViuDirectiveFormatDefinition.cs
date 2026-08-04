using System.ComponentModel.Composition;
using System.Windows.Media;

using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Default appearance of directive attribute names: <c>v-*</c>, <c>:bind</c>, <c>@event</c>, and <c>#slot</c>.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = ViuClassificationTypeNames.Directive)]
[Name(ViuClassificationTypeNames.Directive)]
[UserVisible(true)]
[Order(After = Priority.Default)]
internal sealed class ViuDirectiveFormatDefinition : ClassificationFormatDefinition
{
    /// <summary>
    /// Initializes the format with its Viu default. The user's own choice, made through
    /// Tools &gt; Options &gt; Fonts and Colors, overrides everything set here.
    /// </summary>
    public ViuDirectiveFormatDefinition()
    {
        this.DisplayName = "Viu — directive";
        this.ForegroundColor = Color.FromRgb(0xC5, 0x86, 0xC0);
    }
}
