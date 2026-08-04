using System.ComponentModel.Composition;
using System.Windows.Media;

using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Default appearance of CSS custom property names (<c>--name</c>) — the theme's own tokens.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = ViuClassificationTypeNames.StyleCustomProperty)]
[Name(ViuClassificationTypeNames.StyleCustomProperty)]
[UserVisible(true)]
[Order(After = Priority.Default)]
internal sealed class ViuStyleCustomPropertyFormatDefinition : ClassificationFormatDefinition
{
    /// <summary>
    /// Initializes the format with its Viu default. The user's own choice, made through
    /// Tools &gt; Options &gt; Fonts and Colors, overrides everything set here.
    /// </summary>
    public ViuStyleCustomPropertyFormatDefinition()
    {
        this.DisplayName = "Viu — style custom property";
        this.ForegroundColor = Color.FromRgb(0x4E, 0xC9, 0xB0);
    }
}
