using System;

namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// Controls how declarations from one CSS-first <c>@theme</c> block participate in resolution and
/// custom-property emission.
/// </summary>
[Flags]
public enum UtilityThemeOptions
{
    /// <summary>The declaration uses normal theme-variable behavior.</summary>
    None = 0,

    /// <summary>Utilities use the authored value directly instead of a theme variable.</summary>
    Inline = 1,

    /// <summary>The declaration is emitted even when no discovered candidate uses it.</summary>
    Static = 2,

    /// <summary>The declaration resolves utilities but is not emitted by this compilation unit.</summary>
    Reference = 4,

    /// <summary>The declaration yields to an existing declaration that is not also a default.</summary>
    Default = 8,
}
