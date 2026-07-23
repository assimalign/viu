using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser.Tests;

/// <summary>Tests the browser host over DOM-free integer node operations.</summary>
[SupportedOSPlatform("browser")]
public sealed class BrowserApplicationTests
{
    [Fact]
    public void Builder_BorrowsConfiguredResolvers_AndAddsTransitionBuiltIns()
    {
        IComponent root = ComponentTree.Element("main");
        IComponentFactory components =
            new ComponentFactory(
            [
                new ComponentRegistration(
                    typeof(ScopedTemplate),
                    static () => new ScopedTemplate(),
                    "ScopedTemplate"),
            ]);
        IServiceProvider services = new EmptyServiceProvider();
        BrowserApplicationBuilder builder =
            BrowserApplication.CreateBuilder(root);

        builder.UseComponentFactory(components);
        builder.UseServiceProvider(services);

        BrowserApplication application = builder.Build();

        application.Context.RootComponent.ShouldBeSameAs(root);
        application.Context.Components.ShouldNotBeSameAs(components);
        application.Context.Components
            .Create<ScopedTemplate>()
            .ShouldBeOfType<ScopedTemplate>();
        application.Context.Components
            .Create<Transition>()
            .ShouldBeOfType<Transition>();
        application.Context.Components
            .Create<TransitionGroup>()
            .ShouldBeOfType<TransitionGroup>();
        application.Context.Components
            .Create<BaseTransition>()
            .ShouldBeOfType<BaseTransition>();
        application.Context.Components
            .Create("Transition")
            .ShouldBeOfType<Transition>();
        application.Context.Components
            .Create("TransitionGroup")
            .ShouldBeOfType<TransitionGroup>();
        application.Context.Services.ShouldBeSameAs(services);
        application.Context.Directives.ShouldBeSameAs(
            BrowserDirectiveResolver.Instance);
    }

    [Fact]
    public void Builder_AllowsApplicationToReplaceBrowserDirectiveResolver()
    {
        IComponent root = ComponentTree.Element("main");
        IDirectiveResolver directives =
            new DirectiveRegistry(
                Array.Empty<KeyValuePair<string, IDirective>>());
        BrowserApplicationBuilder builder =
            BrowserApplication.CreateBuilder(root);
        builder.UseComponentFactory(
            new ComponentFactory(Array.Empty<ComponentRegistration>()));
        builder.UseServiceProvider(new EmptyServiceProvider());
        builder.UseDirectiveResolver(directives);

        BrowserApplication application = builder.Build();

        application.Context.Directives.ShouldBeSameAs(directives);
    }

    [Fact]
    public async Task MountAsync_InstallsPluginThenInitializesClearsAndRenders()
    {
        List<string> order = [];
        RecordingHost host = new(order);
        ApplicationContext context = CreateContext(
            ComponentTree.Element(
                "main",
                children: [ComponentTree.Text("hello")]));
        BrowserApplication application = new(
            RendererFactory.CreateRenderer(host.CreateOptions()),
            context,
            initialize: _ =>
            {
                order.Add("initialize");
                return Task.CompletedTask;
            },
            clearContainer: _ => order.Add("clear"));
        CountingPlugin plugin = new(order);
        application.Use(plugin);

        IComponentContext? rootContext =
            await application.MountAsync(7);

        rootContext.ShouldBeNull();
        application.IsMounted.ShouldBeTrue();
        plugin.SeenApplication.ShouldBeSameAs(application);
        order.ShouldBe(
        [
            "plugin",
            "initialize",
            "clear",
            "create:main",
            "create-text:hello",
            "insert:2:1:0",
            "insert:1:7:0",
        ]);

        application.Unmount();

        application.IsMounted.ShouldBeFalse();
        host.Removed.ShouldBe([1]);
    }

    [Fact]
    public async Task MountAsync_SecondMountWarnsAndDoesNotInitializeAgain()
    {
        int initializationCount = 0;
        List<string> warnings = [];
        RecordingHost host = new([]);
        ApplicationContext context =
            CreateContext(ComponentTree.Element("main"));
        context.WarnHandler = warnings.Add;
        BrowserApplication application = new(
            RendererFactory.CreateRenderer(host.CreateOptions()),
            context,
            initialize: _ =>
            {
                initializationCount++;
                return Task.CompletedTask;
            },
            clearContainer: static _ => { });

        await application.MountAsync(7);
        await application.MountAsync(8);

        initializationCount.ShouldBe(1);
        warnings.ShouldContain(
            warning => warning.Contains("already mounted", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MountAsync_TemplateRootReturnsContextAndStampsScopeIdentifier()
    {
        List<string> order = [];
        RecordingHost host = new(order);
        ComponentFactory components = new(
        [
            new ComponentRegistration(
                typeof(ScopedTemplate),
                static () => new ScopedTemplate(),
                "ScopedTemplate"),
        ]);
        ApplicationContext context = new(
            ComponentTree.Template<ScopedTemplate>(),
            components,
            new EmptyServiceProvider());
        BrowserApplication application = new(
            RendererFactory.CreateRenderer(host.CreateOptions()),
            context,
            initialize: static _ => Task.CompletedTask,
            clearContainer: static _ => { });

        IComponentContext? rootContext =
            await application.MountAsync(7);

        rootContext.ShouldNotBeNull();
        rootContext!.Components.ShouldBeSameAs(components);
        order.ShouldContain("scope:1:data-v-browser-test");
    }

    [Fact]
    public void Mount_WhenInitializationIsAsynchronous_RequiresMountAsync()
    {
        TaskCompletionSource initialization =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        RecordingHost host = new([]);
        BrowserApplication application = new(
            RendererFactory.CreateRenderer(host.CreateOptions()),
            CreateContext(ComponentTree.Element("main")),
            initialize: _ => initialization.Task,
            clearContainer: static _ => { });

        InvalidOperationException exception =
            Should.Throw<InvalidOperationException>(() => application.Mount(7));

        exception.Message.ShouldContain("MountAsync");
        application.IsMounted.ShouldBeFalse();
    }

    [Fact]
    public async Task SelectorMount_InitializesBeforeResolvingSelector()
    {
        List<string> order = [];
        RecordingHost host = new(order);
        BrowserApplication application = new(
            RendererFactory.CreateRenderer(host.CreateOptions()),
            CreateContext(ComponentTree.Element("main")),
            initialize: _ =>
            {
                order.Add("initialize");
                return Task.CompletedTask;
            },
            clearContainer: _ => order.Add("clear"),
            resolveContainer: selector =>
            {
                order.Add($"resolve:{selector}");
                return 9;
            });

        await application.MountAsync("#application");

        order[0].ShouldBe("initialize");
        order[1].ShouldBe("resolve:#application");
        order[2].ShouldBe("clear");
    }

    [Fact]
    public async Task HydrationMount_AdoptsServerNodeWithoutClearingContainer()
    {
        int clearCount = 0;
        RecordingHost host = new([]);
        SingleElementHydrationReader hydrationReader =
            new(container: 7, element: 42, tag: "MAIN");
        BrowserApplication application = new(
            RendererFactory.CreateRenderer(
                host.CreateOptions(hydrationReader)),
            CreateContext(ComponentTree.Element("main")),
            hydrate: true,
            initialize: static _ => Task.CompletedTask,
            clearContainer: _ => clearCount++);

        IComponentContext? context =
            await application.MountAsync(7);

        context.ShouldBeNull();
        application.IsHydrating.ShouldBeTrue();
        clearCount.ShouldBe(0);
        host.HydrationSnapshotCount.ShouldBe(1);
        host.Removed.ShouldBeEmpty();
        host.Order.ShouldNotContain("create:main");

        application.Unmount();

        host.Removed.ShouldBe([42]);
    }

    private static ApplicationContext CreateContext(IComponent root)
    {
        return new ApplicationContext(
            root,
            new ComponentFactory(Array.Empty<ComponentRegistration>()),
            new EmptyServiceProvider());
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            ArgumentNullException.ThrowIfNull(serviceType);
            return null;
        }
    }

    private sealed class CountingPlugin(List<string> order) : IApplicationPlugin
    {
        public IApplication? SeenApplication { get; private set; }

        public ValueTask InstallAsync(
            IApplication application,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SeenApplication = application;
            order.Add("plugin");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingHost(List<string> order)
    {
        private int _nextNode;
        private readonly Dictionary<int, int> _parents = [];

        internal List<int> Removed { get; } = [];

        internal IReadOnlyList<string> Order => order;

        internal int HydrationSnapshotCount { get; private set; }

        internal RendererOptions<int> CreateOptions(
            HydrationNodeReader<int>? hydrationReader = null)
        {
            Func<int, HydrationNodeReader<int>>? createHydrationReader =
                hydrationReader is null
                    ? null
                    : _ =>
                    {
                        HydrationSnapshotCount++;
                        return hydrationReader;
                    };
            return new RendererOptions<int>
            {
                Insert = (child, parent, anchor) =>
                {
                    _parents[child] = parent;
                    order.Add($"insert:{child}:{parent}:{anchor}");
                },
                Remove = node =>
                {
                    Removed.Add(node);
                    _parents.Remove(node);
                },
                CreateElement = (tag, _) =>
                {
                    int node = ++_nextNode;
                    order.Add($"create:{tag}");
                    return node;
                },
                CreateText = text =>
                {
                    int node = ++_nextNode;
                    order.Add($"create-text:{text}");
                    return node;
                },
                CreateComment = _ => ++_nextNode,
                SetText = static (_, _) => { },
                ParentNode = node =>
                    _parents.TryGetValue(node, out int parent)
                        ? parent
                        : default,
                NextSibling = static _ => default,
                PatchAttribute = static (_, _, _, _, _, _) => { },
                SetScopeIdentifier = (node, scopeIdentifier) =>
                    order.Add($"scope:{node}:{scopeIdentifier}"),
                CreateHydrationReader = createHydrationReader,
            };
        }
    }

    private sealed class SingleElementHydrationReader(
        int container,
        int element,
        string tag) : HydrationNodeReader<int>
    {
        public override HydrationNodeKind Kind(int node)
            => node == container || node == element
                ? HydrationNodeKind.Element
                : HydrationNodeKind.Other;

        public override int FirstChild(int node)
            => node == container ? element : 0;

        public override int NextSibling(int node) => 0;

        public override int ParentNode(int node)
            => node == element ? container : 0;

        public override string ElementTag(int node)
            => node == element ? tag : "ROOT";

        public override string Data(int node) => string.Empty;

        public override string? Attribute(int node, string name) => null;
    }

    private sealed class ScopedTemplate : IComponentTemplate
    {
        public string? ScopeIdentifier => "data-v-browser-test";

        public ComponentRenderer Setup(IComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return static () => ComponentTree.Element("section");
        }
    }
}
