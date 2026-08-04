using System.ComponentModel.Composition;
using System.Windows.Media;

using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Default appearance of the <c>{{</c> and <c>}}</c> that open and close an interpolation. Bold, so the boundary between markup and expression is visible at a glance while the expression inside colors as ordinary C#.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = ViuClassificationTypeNames.InterpolationDelimiter)]
[Name(ViuClassificationTypeNames.InterpolationDelimiter)]
[UserVisible(true)]
[Order(After = Priority.Default)]
internal sealed class ViuInterpolationDelimiterFormatDefinition : ClassificationFormatDefinition
{
    /// <summary>
    /// Initializes the format with its Viu default. The user's own choice, made through
    /// Tools &gt; Options &gt; Fonts and Colors, overrides everything set here.
    /// </summary>
    public ViuInterpolationDelimiterFormatDefinition()
    {
        this.DisplayName = "Viu — interpolation delimiter";
        this.ForegroundColor = Color.FromRgb(0xFF, 0xD8, 0x66);
        this.IsBold = true;
    }
}
