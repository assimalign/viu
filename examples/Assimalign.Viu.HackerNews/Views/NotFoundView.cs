using System;

using Assimalign.Viu.RuntimeCore;

namespace Assimalign.Viu.HackerNews;

/// <summary>
/// The catch-all not-found route (<c>/:pathMatch(.*)*</c>).
/// </summary>
internal sealed class NotFoundView : IComponentDefinition
{
    /// <summary>The shared route-view definition instance.</summary>
    public static readonly NotFoundView Instance = new();

    private NotFoundView()
    {
    }

    /// <inheritdoc />
    public string? Name => "NotFoundView";

    /// <inheritdoc />
    public Func<VirtualNode?> Setup(ComponentProperties properties, ComponentSetupContext context)
        => () => VirtualNodeFactory.Element(
            "section",
            VirtualNodeFactory.Properties(("class", "hn-view hn-notfound-view")),
            Ui.Text("h1", "hn-view-title", "404"),
            Ui.Text("p", "hn-empty", "That page doesn’t exist."),
            Ui.Link("/top", "hn-back-link", "← Back to top stories"));
}
