using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
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

        builder.AddComponentFactory(components);
        builder.AddServiceProvider(services);

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
        builder.AddComponentFactory(
            new ComponentFactory(Array.Empty<ComponentRegistration>()));
        builder.AddServiceProvider(new EmptyServiceProvider());
        builder.AddDirectiveResolver(directives);

        BrowserApplication application = builder.Build();

        application.Context.Directives.ShouldBeSameAs(directives);
    }

    [Fact]
    public void ApplicationFacade_DefaultResolversBuildPrimitiveApplicationAndPreserveBuilderType()
    {
        IComponent root = ComponentTree.Element("main");

        BrowserApplicationBuilder builder = Application
            .CreateBuilder()
            .AddRootComponent(root)
            .ConfigureApplication(options =>
                options.WarnHandler = static _ => { });
        BrowserApplication application = builder.Build();

        application.Context.RootComponent.ShouldBeSameAs(root);
        application.Context.Components.ShouldNotBeNull();
        application.Context.Services.ShouldNotBeNull();
        application.Context.WarnHandler.ShouldNotBeNull();
    }

    [Fact]
    public async Task RunAsync_DefaultMountTargetResolvesAppAfterBrowserInitialization()
    {
        List<string> order = [];
        TaskCompletionSource mounted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        RecordingHost host = new(order);
        BrowserApplication application = new(
            RendererFactory.CreateRenderer(host.CreateOptions()),
            CreateContext(ComponentTree.Element("main")),
            initialize: _ =>
            {
                order.Add("initialize");
                return Task.CompletedTask;
            },
            clearContainer: _ =>
            {
                order.Add("clear");
                mounted.TrySetResult();
            },
            resolveContainer: selector =>
            {
                order.Add($"resolve:{selector}");
                return 9;
            });

        Task execution = application.RunAsync().AsTask();

        await mounted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        execution.IsCompleted.ShouldBeFalse();
        order[0].ShouldBe("initialize");
        order[1].ShouldBe("resolve:#app");
        order[2].ShouldBe("clear");

        await application.StopAsync();
        await execution;
    }

    [Fact]
    public async Task MountAsync_LowerLevelPathInitializesClearsRendersAndStops()
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
        IComponentContext? rootContext =
            await application.MountAsync(7);

        rootContext.ShouldBeNull();
        application.IsRunning.ShouldBeTrue();
        order.ShouldBe(
        [
            "initialize",
            "clear",
            "create:main",
            "create-text:hello",
            "insert:2:1:0",
            "insert:1:7:0",
        ]);

        await application.StopAsync();

        application.IsRunning.ShouldBeFalse();
        host.Removed.ShouldBe([1]);
    }

    [Fact]
    public async Task MountAsync_SecondMountThrowsAndDoesNotInitializeAgain()
    {
        int initializationCount = 0;
        RecordingHost host = new([]);
        ApplicationContext context =
            CreateContext(ComponentTree.Element("main"));
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
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await application.MountAsync(8));

        initializationCount.ShouldBe(1);
        await application.StopAsync();
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
        await application.StopAsync();
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
        application.IsRunning.ShouldBeFalse();
        initialization.SetResult();
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
        await application.StopAsync();
    }

    [Fact]
    public async Task SelectorMount_ClaimsExecutionBeforeInitializationCompletes()
    {
        TaskCompletionSource initializationStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource initializationRelease =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        int resolutionCount = 0;
        RecordingHost host = new([]);
        BrowserApplication application = new(
            RendererFactory.CreateRenderer(host.CreateOptions()),
            CreateContext(ComponentTree.Element("main")),
            initialize: async _ =>
            {
                initializationStarted.TrySetResult();
                await initializationRelease.Task.ConfigureAwait(false);
            },
            clearContainer: static _ => { },
            resolveContainer: _ =>
            {
                resolutionCount++;
                return 9;
            });

        ValueTask<IComponentContext?> mounting =
            application.MountAsync("#application");

        Should.Throw<InvalidOperationException>(() =>
            application.Use(static (executionContext, next) =>
                next(executionContext)));
        await initializationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        resolutionCount.ShouldBe(0);

        initializationRelease.TrySetResult();
        await mounting;
        resolutionCount.ShouldBe(1);
        await application.StopAsync();
    }

    [Fact]
    public async Task SelectorMount_InitializationFailureConsumesExecutionClaim()
    {
        int initializationCount = 0;
        int resolutionCount = 0;
        RecordingHost host = new([]);
        BrowserApplication application = new(
            RendererFactory.CreateRenderer(host.CreateOptions()),
            CreateContext(ComponentTree.Element("main")),
            initialize: _ =>
            {
                initializationCount++;
                return Task.FromException(
                    new InvalidOperationException("initialization failed"));
            },
            clearContainer: static _ => { },
            resolveContainer: _ =>
            {
                resolutionCount++;
                return 9;
            });

        InvalidOperationException exception =
            await Should.ThrowAsync<InvalidOperationException>(
                async () => await application.MountAsync("#application"));

        exception.Message.ShouldBe("initialization failed");
        Should.Throw<InvalidOperationException>(() =>
        {
            _ = application.MountAsync("#other");
        });
        initializationCount.ShouldBe(1);
        resolutionCount.ShouldBe(0);
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

        await application.StopAsync();

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
