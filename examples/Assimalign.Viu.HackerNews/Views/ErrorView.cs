using System;
using System.Collections.Generic;

using Assimalign.Viu;

namespace Assimalign.Viu.HackerNews;

/// <summary>
/// An error banner, used both inline (with a <c>message</c> prop, when a store's load fails) and as
/// the <c>ErrorComponent</c> of the async route components (which passes the failure as an
/// <c>error</c> prop, per <see cref="AsyncComponents"/>).
/// </summary>
internal sealed class ErrorView : IComponent
{
    /// <summary>The shared error definition instance.</summary>
    public static readonly ErrorView Instance = new();

    private ErrorView()
    {
    }

    /// <inheritdoc />
    public string? Name => "ErrorView";

    /// <inheritdoc />
    public IReadOnlyList<ComponentPropertyDefinition>? Properties { get; } =
    [
        new ComponentPropertyDefinition("message"),
        new ComponentPropertyDefinition("error"),
    ];

    /// <summary>Builds an inline error banner vnode with <paramref name="message"/>.</summary>
    /// <param name="message">The message to show.</param>
    public static VirtualNode Inline(string message)
        => VirtualNodeFactory.Component(Instance, VirtualNodeFactory.Properties(("message", message)));

    /// <inheritdoc />
    public ComponentSetup Setup(ComponentProperties properties, ComponentSetupContext context)
        => () =>
        {
            var message = properties.Get<string>("message")
                ?? (properties.Get<Exception>("error"))?.Message
                ?? "Something went wrong.";
            return VirtualNodeFactory.Element(
                "div",
                VirtualNodeFactory.Properties(("class", "hn-error"), ("role", "alert")),
                Ui.Text("strong", null, "Couldn’t load this page. "),
                Ui.Raw(message));
        };
}
