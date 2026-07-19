using System;
using System.Collections.Generic;
using System.Runtime.Versioning;

using Shouldly;
using Xunit;

using Assimalign.Viu.RuntimeCore;

namespace Assimalign.Viu.RuntimeDom.Tests;

// Pins the [V01.01.04.04.01] application API surface on BrowserApplication against Vue's app object
// (https://vuejs.org/api/application.html): chainable Component/Directive/Provide/Use/Config, each
// delegating to the wrapped Application<int>. Registration and config need no browser — the
// wrapper is constructed over an inert int renderer (its interop members Mount/Unmount are the
// browser-dependent remainder, verified through the demo app). The platform annotation mirrors the
// type under test; nothing here crosses the interop boundary.
[SupportedOSPlatform("browser")]
public sealed class BrowserApplicationTests
{
    private static BrowserApplication CreateApp(out Application<int> wrapped)
    {
        // An inert int-handle renderer: the registration surface never invokes node operations.
        var renderer = RendererFactory.CreateRenderer(new RendererOptions<int>
        {
            Insert = static (_, _, _) => { },
            Remove = static _ => { },
            CreateElement = static (_, _) => 1,
            CreateText = static _ => 1,
            CreateComment = static _ => 1,
            SetText = static (_, _) => { },
            SetElementText = static (_, _) => { },
            ParentNode = static _ => 0,
            NextSibling = static _ => 0,
            PatchProperty = static (_, _, _, _, _, _) => { },
        });
        wrapped = renderer.CreateApplication(new RenderComponent(static () => VirtualNodeFactory.Element("div")));
        return new BrowserApplication(wrapped);
    }

    [Fact]
    public void Component_RegistersAndResolves_ThroughTheWrappedApplication()
    {
        var app = CreateApp(out var wrapped);
        var child = new RenderComponent(static () => VirtualNodeFactory.Element("span"));

        var returned = app.Component("MyChild", child);

        returned.ShouldBeSameAs(app); // chainable, returns BrowserApplication
        app.Component("MyChild").ShouldBeSameAs(child);
        wrapped.Component("MyChild").ShouldBeSameAs(child); // delegated to the wrapped app
        app.Component("Unknown").ShouldBeNull();
    }

    [Fact]
    public void Directive_RegistersAndResolves_ThroughTheWrappedApplication()
    {
        var app = CreateApp(out var wrapped);
        var focus = new Directive { Mounted = static (_, _, _, _) => { } };

        var returned = app.Directive("focus", focus);

        returned.ShouldBeSameAs(app);
        app.Directive("focus").ShouldBeSameAs(focus);
        wrapped.Directive("focus").ShouldBeSameAs(focus);
        app.Directive("missing").ShouldBeNull();
    }

    [Fact]
    public void Provide_TypedAndString_DelegateToTheWrappedApplication()
    {
        var app = CreateApp(out _);
        var key = new InjectionKey<string>("theme");
        var warnings = new List<string>();
        var previousSink = RuntimeWarnings.Sink;
        RuntimeWarnings.Sink = warnings.Add;
        try
        {
            app.Provide(key, "dark").ShouldBeSameAs(app);
            app.Provide("locale", "en-US").ShouldBeSameAs(app);

            // Re-providing the same key warns through the wrapped app — proof the provide landed in
            // the shared ApplicationContext (upstream: "App already provides property with key").
            app.Provide(key, "light");
            warnings.ShouldContain(message => message.Contains("already provides"));
        }
        finally
        {
            RuntimeWarnings.Sink = previousSink;
        }
    }

    [Fact]
    public void Use_InstallsThePluginOnce_AgainstTheWrappedApplication()
    {
        var app = CreateApp(out var wrapped);
        var plugin = new CountingPlugin();

        app.Use(plugin, options: "options").ShouldBeSameAs(app);
        app.Use(plugin).ShouldBeSameAs(app); // repeat Use of the same instance is deduplicated

        plugin.InstallCount.ShouldBe(1);
        plugin.SeenApplication.ShouldBeSameAs(wrapped); // the plugin receives the wrapped Application<int>
        plugin.SeenOptions.ShouldBe("options");
    }

    [Fact]
    public void Config_IsTheWrappedApplicationsConfiguration()
    {
        var app = CreateApp(out var wrapped);

        app.Config.ShouldBeSameAs(wrapped.Config); // one shared ApplicationConfiguration

        Action<string> warnHandler = static _ => { };
        app.Config.WarnHandler = warnHandler;
        wrapped.Config.WarnHandler.ShouldBeSameAs(warnHandler);
    }

    [Fact]
    public void RegistrationSurface_ChainsFluently()
    {
        var app = CreateApp(out _);
        var child = new RenderComponent(static () => VirtualNodeFactory.Element("span"));
        var focus = new Directive { Mounted = static (_, _, _, _) => { } };

        var returned = app
            .Component("Child", child)
            .Directive("focus", focus)
            .Provide("locale", "en-US")
            .Use(new CountingPlugin());

        returned.ShouldBeSameAs(app);
    }

    private sealed class CountingPlugin : IPlugin<int>
    {
        public int InstallCount { get; private set; }

        public Application<int>? SeenApplication { get; private set; }

        public object? SeenOptions { get; private set; }

        public void Install(Application<int> application, object? options)
        {
            InstallCount++;
            SeenApplication = application;
            SeenOptions = options;
        }
    }
}
