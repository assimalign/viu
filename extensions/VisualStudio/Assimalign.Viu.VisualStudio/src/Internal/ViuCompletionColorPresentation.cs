namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Carries the authored CSS value and its parsed swatch color together.
/// </summary>
internal sealed class ViuCompletionColorPresentation
{
    /// <summary>Initializes a candidate-specific swatch presentation.</summary>
    internal ViuCompletionColorPresentation(string cssValue, ViuCompletionColor color)
    {
        this.CssValue = cssValue;
        this.Color = color;
    }

    /// <summary>Gets the concrete CSS value transported by the language server.</summary>
    internal string CssValue { get; }

    /// <summary>Gets the device-sRGB color used for the Visual Studio image.</summary>
    internal ViuCompletionColor Color { get; }
}
