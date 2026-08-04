using System.ComponentModel.Composition;
using System.Windows.Media;

using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Default appearance of tag punctuation: <c>&lt;</c>, <c>&gt;</c>, <c>&lt;/</c>, <c>/&gt;</c>, and the attribute <c>=</c>. Deliberately muted, so tag structure recedes and names carry the reading.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = ViuClassificationTypeNames.Delimiter)]
[Name(ViuClassificationTypeNames.Delimiter)]
[UserVisible(true)]
[Order(After = Priority.Default)]
internal sealed class ViuDelimiterFormatDefinition : ClassificationFormatDefinition
{
    /// <summary>
    /// Initializes the format with its Viu default. The user's own choice, made through
    /// Tools &gt; Options &gt; Fonts and Colors, overrides everything set here.
    /// </summary>
    public ViuDelimiterFormatDefinition()
    {
        this.DisplayName = "Viu — tag delimiter";
        this.ForegroundColor = Color.FromRgb(0x6E, 0x76, 0x81);
    }
}
