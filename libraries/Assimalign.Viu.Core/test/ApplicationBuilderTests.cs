using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.State;

namespace Assimalign.Viu.Tests;

public sealed class ApplicationBuilderTests
{
    [Fact]
    public void Build_AddMethodsFreezeBorrowedCompositionBeforeRuntimeUse()
    {
        EmptyServiceProvider services = new();
        ComponentFactory components = new(Array.Empty<ComponentRegistration>());
        IComponent root = ComponentTree.Element("main");
        StubStateStoreRegistry state = new();
        DirectiveRegistry directives = new(
            Array.Empty<KeyValuePair<string, IDirective>>());
        TestApplicationBuilder builder = new();

        TestApplicationBuilder configured = builder
            .AddRootComponent(root)
            .AddComponentFactory(components)
            .AddServiceProvider(services)
            .AddStateRegistry(state)
            .AddDirectiveResolver(directives);
        TestApplication application = configured.Build();

        // [APP-2] Add* composes before Build; runtime Use has no composition role.
        configured.ShouldBeSameAs(builder);
        application.Context.RootComponent.ShouldBeSameAs(root);
        application.Context.Components.ShouldBeSameAs(components);
        application.Context.Services.ShouldBeSameAs(services);
        application.Context.State.ShouldBeSameAs(state);
        application.Context.Directives.ShouldBeSameAs(directives);
    }

    [Fact]
    public void Build_PrimitiveRootUsesEmptyDefaultResolvers()
    {
        TestApplication application = new TestApplicationBuilder()
            .AddRootComponent(ComponentTree.Element("main"))
            .Build();

        application.Context.Services.GetService(typeof(object)).ShouldBeNull();
        Should.Throw<InvalidOperationException>(
            () => application.Context.Components.Create(typeof(UnknownTemplate)));
    }

    [Fact]
    public void Build_ApplicationOptionsAreFrozenIntoEachContext()
    {
        List<string> warnings = [];
        Action<string> warnHandler = warnings.Add;
        Action<Exception, IComponentContext?, string> errorHandler =
            static (_, _, _) => { };
        ApplicationOptions? configuredOptions = null;
        TestApplicationBuilder builder = new TestApplicationBuilder()
            .AddRootComponent(ComponentTree.Element("main"))
            .ConfigureApplication(options =>
            {
                configuredOptions = options;
                options.WarnHandler = warnHandler;
                options.ErrorHandler = errorHandler;
            });

        TestApplication application = builder.Build();
        configuredOptions!.WarnHandler = static _ => { };
        configuredOptions.ErrorHandler = null;

        application.Context.WarnHandler.ShouldBeSameAs(warnHandler);
        application.Context.ErrorHandler.ShouldBeSameAs(errorHandler);
    }

    private sealed class TestApplicationBuilder : ApplicationBuilder
    {
        public override TestApplicationBuilder AddRootComponent(IComponent component)
        {
            base.AddRootComponent(component);
            return this;
        }

        public override TestApplicationBuilder AddComponentFactory(
            IComponentFactory components)
        {
            base.AddComponentFactory(components);
            return this;
        }

        public override TestApplicationBuilder AddServiceProvider(
            IServiceProvider services)
        {
            base.AddServiceProvider(services);
            return this;
        }

        public override TestApplicationBuilder AddStateRegistry(
            IStateStoreRegistry state)
        {
            base.AddStateRegistry(state);
            return this;
        }

        public override TestApplicationBuilder AddDirectiveResolver(
            IDirectiveResolver directives)
        {
            base.AddDirectiveResolver(directives);
            return this;
        }

        public override TestApplicationBuilder ConfigureApplication(
            Action<ApplicationOptions> configure)
        {
            base.ConfigureApplication(configure);
            return this;
        }

        internal TestApplication Build()
        {
            return new TestApplication(CreateContext());
        }
    }

    private sealed class TestApplication : Application<int>
    {
        internal TestApplication(IApplicationContext context)
            : base(context)
        {
        }

        protected override ValueTask<int> ResolveMountTargetAsync(
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(1);
        }

        protected override IComponentContext? MountCore(int container)
        {
            return null;
        }

        protected override void UnmountCore()
        {
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return null;
        }
    }

    private sealed class StubStateStoreRegistry : IStateStoreRegistry
    {
        public int Count => 0;

        public bool IsDisposed { get; private set; }

        public TStore GetOrCreate<TStore>(
            StateStoreDefinition<TStore> definition,
            IComponentContext? owner = null)
            where TStore : class
        {
            throw new InvalidOperationException("No stores are configured for this test.");
        }

        public bool Remove<TStore>(StateStoreDefinition<TStore> definition)
            where TStore : class
        {
            return false;
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed class UnknownTemplate : IComponentTemplate
    {
        public ComponentRenderer Setup(IComponentContext context)
        {
            return static () => ComponentTree.Comment();
        }
    }
}
