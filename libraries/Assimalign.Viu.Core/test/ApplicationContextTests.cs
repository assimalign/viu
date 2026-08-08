using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.State;

namespace Assimalign.Viu.Core.Tests;

public sealed class ApplicationContextTests
{
    [Fact]
    public void Constructor_OptionsLaterMutate_RetainsBorrowedSnapshot()
    {
        var root = new CommentNode("root");
        var components = new ComponentFactory();
        var directives = new DirectiveRegistry(
            Array.Empty<KeyValuePair<Type, IDirective>>());
        Action<string> warningHandler = static _ => { };
        Action<ComponentContext, string, IReadOnlyList<object?>> eventObserver =
            static (_, _, _) => { };
        var options = new ApplicationOptions
        {
            RootComponent = root,
            Components = components,
            Directives = directives,
            WarnHandler = warningHandler,
            EventObserver = eventObserver,
        };

        var context = new ApplicationContext(options);
        options.RootComponent = new CommentNode("replacement");
        options.Components = new ComponentFactory();
        options.Directives = null;
        options.WarnHandler = null;
        options.EventObserver = null;

        context.RootComponent.ShouldBeSameAs(root);
        context.Components.ShouldBeSameAs(components);
        context.Directives.ShouldBeSameAs(directives);
        context.WarnHandler.ShouldBeSameAs(warningHandler);
        context.EventObserver.ShouldBeSameAs(eventObserver);
    }

    [Fact]
    public void Constructor_NoServicesAndNoState_PreservesNullableOptIn()
    {
        var context = new ApplicationContext(
            new ApplicationOptions { RootComponent = new CommentNode("root") });

        context.Services.ShouldBeNull();
        context.State.ShouldBeNull();
    }

    [Fact]
    public void Services_ConfiguredStateWinsAndOtherRequestsDelegate()
    {
        var configuredState = new TestStateStoreRegistry();
        var providerState = new TestStateStoreRegistry();
        var marker = new ServiceMarker();
        var services = new TestServiceProvider(
            new Dictionary<Type, object>
            {
                [typeof(IStateStoreRegistry)] = providerState,
                [typeof(ServiceMarker)] = marker,
            });
        var context = new ApplicationContext(
            new ApplicationOptions
            {
                RootComponent = new CommentNode("root"),
                Services = services,
                State = configuredState,
            });

        context.Services.ShouldNotBeNull();
        context.Services!.GetService(typeof(IStateStoreRegistry)).ShouldBeSameAs(configuredState);
        context.Services!.GetService(typeof(ServiceMarker)).ShouldBeSameAs(marker);
        context.State.ShouldBeSameAs(configuredState);
    }

    [Fact]
    public void Services_StateWithoutExternalProvider_SynthesizesOnlyRegistryResolution()
    {
        var state = new TestStateStoreRegistry();
        var context = new ApplicationContext(
            new ApplicationOptions
            {
                RootComponent = new CommentNode("root"),
                State = state,
            });

        context.Services.ShouldNotBeNull();
        context.Services!.GetService(typeof(IStateStoreRegistry)).ShouldBeSameAs(state);
        context.Services!.GetService(typeof(ServiceMarker)).ShouldBeNull();
    }

    private sealed class ServiceMarker
    {
    }

    private sealed class TestServiceProvider : IServiceProvider
    {
        private readonly IReadOnlyDictionary<Type, object> _services;

        internal TestServiceProvider(IReadOnlyDictionary<Type, object> services)
        {
            _services = services;
        }

        public object? GetService(Type serviceType) =>
            _services.TryGetValue(serviceType, out object? service) ? service : null;
    }

    private sealed class TestStateStoreRegistry : IStateStoreRegistry
    {
        public int Count => 0;

        public bool IsDisposed { get; private set; }

        public TStore GetOrCreate<TStore>(StateStoreDefinition<TStore> definition)
            where TStore : class =>
            throw new NotSupportedException();

        public bool Remove<TStore>(StateStoreDefinition<TStore> definition)
            where TStore : class => false;

        public void Dispose() => IsDisposed = true;
    }
}
