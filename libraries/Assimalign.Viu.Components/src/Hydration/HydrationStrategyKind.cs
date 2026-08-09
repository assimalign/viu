namespace Assimalign.Viu.Components;

/// <summary>Identifies when an adopted server-rendered component becomes interactive.</summary>
/// <remarks>
/// The value is immutable invocation metadata. Core interprets it only through a host trigger
/// seam, so Browser APIs never enter the component model. Specified by <c>[HYD-LAZY-1]</c>.
/// </remarks>
public enum HydrationStrategyKind
{
    /// <summary>Activates the component during the initial hydration walk.</summary>
    Immediate = 0,

    /// <summary>Activates the component when the host reports an idle turn.</summary>
    Idle = 1,

    /// <summary>Activates the component when its adopted markup becomes visible.</summary>
    Visible = 2,

    /// <summary>Activates the component when a host media condition matches.</summary>
    MediaQuery = 3,

    /// <summary>Activates the component on the first configured interaction.</summary>
    Interaction = 4,
}
