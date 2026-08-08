using System;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser.Tests;

// Pins Browser composition defaults and public host bootstrap configuration [APP-2], [HYD-2].
public sealed class BrowserApplicationBuilderTests
{
    [Fact]
    public async Task ConfigureBrowser_HydrationAndSelector_AreSnapshottedByBuild()
    {
        BrowserApplication application = new BrowserApplicationBuilder()
            .ConfigureApplication(
                options => options.RootComponent = new TextNode("root"))
            .ConfigureBrowser(
                options =>
                {
                    options.Hydrate = true;
                    options.MountTargetSelector = "#shell";
                })
            .Build();

        application.IsHydrating.ShouldBeTrue();
        application.MountTargetSelector.ShouldBe("#shell");
        await application.DisposeAsync();
    }

    [Fact]
    public async Task Build_Defaults_ResolveBrowserDirectivesAndTransitionComponents()
    {
        ComponentRegistration customRegistration = ComponentRegistration.Define(
            "Custom",
            new ComponentContract(),
            _ => _ => new TextNode("custom"));
        var customComponents = new ComponentFactory();
        customComponents.Register(customRegistration);
        BrowserApplication application = new BrowserApplicationBuilder()
            .ConfigureApplication(
                options =>
                {
                    options.RootComponent = new TextNode("root");
                    options.Components = customComponents;
                })
            .Build();

        application.Context.Directives.ShouldNotBeNull();
        application.Context.Directives!
            .Resolve(typeof(VShow))
            .ShouldBeSameAs(VShow.Instance);
        application.Context.Components
            .Resolve(ComponentReference.ForType(typeof(Transition)))
            .ShouldBeSameAs(Transition.Registration);
        application.Context.Components
            .Resolve(ComponentReference.ForName("transition-group"))
            .ShouldBeSameAs(TransitionGroup.Registration);
        application.Context.Components
            .Resolve(customRegistration.Reference)
            .ShouldBeSameAs(customRegistration);
        await application.DisposeAsync();
    }

    [Fact]
    public async Task Build_CustomDirectiveResolver_IsPreserved()
    {
        var customDirectives = new CustomDirectiveResolver();
        BrowserApplication application = new BrowserApplicationBuilder()
            .ConfigureApplication(
                options =>
                {
                    options.RootComponent = new TextNode("root");
                    options.Directives = customDirectives;
                })
            .Build();

        application.Context.Directives.ShouldBeSameAs(customDirectives);
        await application.DisposeAsync();
    }

    [Fact]
    public void Build_MissingRootComponent_ThrowsBeforeCreatingApplication()
    {
        var builder = new BrowserApplicationBuilder();

        Action build = () => builder.Build();

        build.ShouldThrow<InvalidOperationException>();
    }

    private sealed class CustomDirectiveResolver : IDirectiveResolver
    {
        public IDirective? Resolve(Type directiveType)
        {
            ArgumentNullException.ThrowIfNull(directiveType);
            return null;
        }
    }
}
