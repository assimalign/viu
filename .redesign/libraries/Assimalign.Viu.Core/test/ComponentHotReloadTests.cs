using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Core.Tests;

public sealed class ComponentHotReloadTests
{
    [Fact]
    public void Classify_RegisteredMarkerSets_UsesConservativePrecedence()
    {
        ComponentHotReload.Register(
            typeof(ClassificationComponent),
            "classification-component",
            typeof(ClassificationTemplateMarker),
            typeof(ClassificationScriptMarker),
            typeof(ClassificationStyleMarker));

        ComponentHotReload.Classify(
            typeof(ClassificationComponent),
            [typeof(ClassificationStyleMarker)])
            .ShouldBe(ComponentHotReloadChangeKind.StyleOnly);
        ComponentHotReload.Classify(
            typeof(ClassificationComponent),
            [typeof(ClassificationStyleMarker), typeof(ClassificationTemplateMarker)])
            .ShouldBe(ComponentHotReloadChangeKind.Template);
        ComponentHotReload.Classify(
            typeof(ClassificationComponent),
            [typeof(ClassificationTemplateMarker), typeof(ClassificationScriptMarker)])
            .ShouldBe(ComponentHotReloadChangeKind.ScriptReset);
        ComponentHotReload.Classify(
            typeof(ClassificationComponent),
            [typeof(ClassificationComponent)])
            .ShouldBe(ComponentHotReloadChangeKind.ScriptReset);
        ComponentHotReload.Classify(typeof(ClassificationComponent), null)
            .ShouldBe(ComponentHotReloadChangeKind.ScriptReset);
        ComponentHotReload.Classify(
            typeof(ClassificationComponent),
            [typeof(UnrelatedMarker)])
            .ShouldBe(ComponentHotReloadChangeKind.None);
    }

    [Fact]
    public void ApplyUpdates_ScriptMarker_RaisesOneResetNotificationForRegisteredComponent()
    {
        ComponentHotReload.Register(
            typeof(NotificationComponent),
            "notification-component",
            typeof(NotificationTemplateMarker),
            typeof(NotificationScriptMarker),
            typeof(NotificationStyleMarker));
        List<(string Identifier, Type ComponentType)> notifications = [];
        ComponentScriptUpdateResetHandler handler = (identifier, componentType) =>
        {
            if (componentType == typeof(NotificationComponent))
            {
                notifications.Add((identifier, componentType));
            }
        };
        ComponentHotReload.ScriptUpdateRequiresReset += handler;

        try
        {
            ComponentHotReload.ApplyUpdates([typeof(NotificationScriptMarker)]);
        }
        finally
        {
            ComponentHotReload.ScriptUpdateRequiresReset -= handler;
        }

        notifications.ShouldHaveSingleItem();
        notifications[0].Identifier.ShouldBe("notification-component");
        notifications[0].ComponentType.ShouldBe(typeof(NotificationComponent));
    }

    private sealed class ClassificationComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) => _ => null;
    }

    private sealed class NotificationComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) => _ => null;
    }

    private sealed class ClassificationTemplateMarker
    {
    }

    private sealed class ClassificationScriptMarker
    {
    }

    private sealed class ClassificationStyleMarker
    {
    }

    private sealed class NotificationTemplateMarker
    {
    }

    private sealed class NotificationScriptMarker
    {
    }

    private sealed class NotificationStyleMarker
    {
    }

    private sealed class UnrelatedMarker
    {
    }
}
