using Assimalign.Viu;

namespace Assimalign.Viu.Browser;

/// <summary>Configures Browser-specific bootstrap behavior independently of application composition.</summary>
/// <remarks>
/// The builder snapshots these values when it builds an application. Composition remains on
/// <see cref="ApplicationOptions"/> as required by <c>[APP-2]</c>; these settings only select the
/// Browser mount target and first-render strategy.
/// </remarks>
public sealed class BrowserApplicationOptions
{
    /// <summary>
    /// Gets or sets whether the first render adopts server-rendered markup through the Browser
    /// hydration snapshot reader. Specified by <c>[HYD-1]</c> through <c>[HYD-4]</c>.
    /// </summary>
    public bool Hydrate { get; set; }

    /// <summary>Gets or sets the non-empty CSS selector resolved by top-level startup.</summary>
    public string MountTargetSelector { get; set; } = "#app";
}
