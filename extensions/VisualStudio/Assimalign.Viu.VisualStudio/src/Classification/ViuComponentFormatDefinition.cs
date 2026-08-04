using System.ComponentModel.Composition;
using System.Windows.Media;

using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Default appearance of component tag names — PascalCase or dotted. Bold, because which tags are components is the single most load-bearing fact about a Viu template.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = ViuClassificationTypeNames.Component)]
[Name(ViuClassificationTypeNames.Component)]
[UserVisible(true)]
[Order(After = Priority.Default)]
internal sealed class ViuComponentFormatDefinition : ClassificationFormatDefinition
{
    /// <summary>
    /// Initializes the format with its Viu default. The user's own choice, made through
    /// Tools &gt; Options &gt; Fonts and Colors, overrides everything set here.
    /// </summary>
    public ViuComponentFormatDefinition()
    {
        this.DisplayName = "Viu — component tag";
        this.ForegroundColor = Color.FromRgb(0x2B, 0xD9, 0xBC);
        this.IsBold = true;
    }
}
