namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// Canonical CSS-first namespace names understood by <see cref="UtilityTheme"/>.
/// </summary>
/// <remarks>
/// Names omit the leading custom-property marker and trailing wildcard. For example,
/// <see cref="Color"/> represents the <c>--color-*</c> namespace.
/// </remarks>
public static class UtilityThemeNamespaceNames
{
    /// <summary>The <c>--color-*</c> namespace.</summary>
    public const string Color = "color";

    /// <summary>The <c>--font-*</c> font-family namespace.</summary>
    public const string FontFamily = "font";

    /// <summary>The <c>--text-*</c> font-size namespace.</summary>
    public const string FontSize = "text";

    /// <summary>The <c>--font-weight-*</c> namespace.</summary>
    public const string FontWeight = "font-weight";

    /// <summary>The <c>--tracking-*</c> letter-spacing namespace.</summary>
    public const string LetterSpacing = "tracking";

    /// <summary>The <c>--leading-*</c> line-height namespace.</summary>
    public const string LineHeight = "leading";

    /// <summary>The <c>--tab-size-*</c> namespace introduced in v4.3.</summary>
    public const string TabSize = "tab-size";

    /// <summary>The <c>--breakpoint-*</c> responsive-variant namespace.</summary>
    public const string Breakpoint = "breakpoint";

    /// <summary>The <c>--container-*</c> query and sizing namespace.</summary>
    public const string Container = "container";

    /// <summary>The <c>--spacing-*</c> namespace and numeric <c>--spacing</c> scale.</summary>
    public const string Spacing = "spacing";

    /// <summary>The <c>--radius-*</c> border-radius namespace.</summary>
    public const string Radius = "radius";

    /// <summary>The <c>--shadow-*</c> box-shadow namespace.</summary>
    public const string Shadow = "shadow";

    /// <summary>The <c>--inset-shadow-*</c> inset box-shadow namespace.</summary>
    public const string InsetShadow = "inset-shadow";

    /// <summary>The <c>--drop-shadow-*</c> filter namespace.</summary>
    public const string DropShadow = "drop-shadow";

    /// <summary>The <c>--text-shadow-*</c> namespace.</summary>
    public const string TextShadow = "text-shadow";

    /// <summary>The <c>--blur-*</c> filter namespace.</summary>
    public const string Blur = "blur";

    /// <summary>The <c>--perspective-*</c> namespace.</summary>
    public const string Perspective = "perspective";

    /// <summary>The <c>--zoom-*</c> namespace introduced in v4.3.</summary>
    public const string Zoom = "zoom";

    /// <summary>The <c>--aspect-*</c> aspect-ratio namespace.</summary>
    public const string AspectRatio = "aspect";

    /// <summary>The <c>--ease-*</c> transition-timing namespace.</summary>
    public const string TransitionTiming = "ease";

    /// <summary>The <c>--animate-*</c> animation namespace.</summary>
    public const string Animation = "animate";
}
