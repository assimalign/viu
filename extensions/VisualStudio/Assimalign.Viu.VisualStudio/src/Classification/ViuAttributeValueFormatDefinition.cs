using System.ComponentModel.Composition;
using System.Windows.Media;

using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Default appearance of attribute values, quotes included. A class attribute is one uninterrupted value: utility classes and their variant prefixes are deliberately not split into separate colors.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = ViuClassificationTypeNames.AttributeValue)]
[Name(ViuClassificationTypeNames.AttributeValue)]
[UserVisible(true)]
[Order(After = Priority.Default)]
internal sealed class ViuAttributeValueFormatDefinition : ClassificationFormatDefinition
{
    /// <summary>
    /// Initializes the format with its Viu default. The user's own choice, made through
    /// Tools &gt; Options &gt; Fonts and Colors, overrides everything set here.
    /// </summary>
    public ViuAttributeValueFormatDefinition()
    {
        this.DisplayName = "Viu — attribute value";
        this.ForegroundColor = Color.FromRgb(0xFF, 0xAB, 0x70);
    }
}
