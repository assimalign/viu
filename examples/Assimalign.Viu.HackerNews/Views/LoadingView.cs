using System;

using Assimalign.Viu.RuntimeCore;

namespace Assimalign.Viu.HackerNews;

/// <summary>
/// A small loading indicator, used both inline (while a store load is in flight) and as the
/// <c>LoadingComponent</c> of the async route components (<see cref="AppRoutes"/>).
/// </summary>
internal sealed class LoadingView : IComponentDefinition
{
    /// <summary>The shared loading definition instance.</summary>
    public static readonly LoadingView Instance = new();

    private LoadingView()
    {
    }

    /// <inheritdoc />
    public string? Name => "LoadingView";

    /// <inheritdoc />
    public Func<VirtualNode?> Setup(ComponentProperties properties, ComponentSetupContext context)
        => () => VirtualNodeFactory.Element(
            "div",
            VirtualNodeFactory.Properties(("class", "hn-loading"), ("role", "status")),
            Ui.Element("span", "hn-spinner"),
            Ui.Text("span", "hn-loading-text", "Loading…"));
}
