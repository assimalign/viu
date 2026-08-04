using System.ComponentModel.Composition;
using System.Windows.Media;

using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Default appearance of selectors in a style block.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = ViuClassificationTypeNames.StyleSelector)]
[Name(ViuClassificationTypeNames.StyleSelector)]
[UserVisible(true)]
[Order(After = Priority.Default)]
internal sealed class ViuStyleSelectorFormatDefinition : ClassificationFormatDefinition
{
    /// <summary>
    /// Initializes the format with its Viu default. The user's own choice, made through
    /// Tools &gt; Options &gt; Fonts and Colors, overrides everything set here.
    /// </summary>
    public ViuStyleSelectorFormatDefinition()
    {
        this.DisplayName = "Viu — style selector";
        this.ForegroundColor = Color.FromRgb(0xD7, 0xBA, 0x7D);
    }
}
