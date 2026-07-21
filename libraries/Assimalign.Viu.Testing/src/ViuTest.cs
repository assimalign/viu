using System;
using System.Collections.Generic;

using Assimalign.Viu;

namespace Assimalign.Viu.Testing;

/// <summary>
/// The entry point for component testing — the C# port of <c>@vue/test-utils</c>'s <c>mount</c>
/// (https://test-utils.vuejs.org/api/#mount). Renders a component against the in-memory test
/// renderer and returns a <see cref="ComponentWrapper"/> for querying, interacting, and asserting,
/// all DOM-free in xUnit.
/// </summary>
public static class ViuTest
{
    /// <summary>
    /// Mounts <paramref name="component"/> with the given <paramref name="options"/> and returns its
    /// wrapper. The caller supplies the component instance (source-generated component factories,
    /// never reflection-based activation, construct definitions in an AOT/trimming-safe build).
    /// Dispose the returned wrapper (a <c>using</c>) to unmount and reset the scheduler.
    /// </summary>
    /// <typeparam name="TComponent">The component definition type (for typed <c>FindComponent</c> matching).</typeparam>
    /// <param name="component">The component definition to mount.</param>
    /// <param name="options">Props, slots, and global config (provides, registered components, stubs), or null.</param>
    /// <returns>The root component wrapper.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="component"/> is null.</exception>
    public static ComponentWrapper Mount<TComponent>(TComponent component, ComponentMountOptions? options = null)
        where TComponent : IComponentDefinition
    {
        ArgumentNullException.ThrowIfNull(component);
        options ??= new ComponentMountOptions();

        // The wrapper owns the scheduler lifecycle for the mount: reset, then capture flushes so
        // async helpers stay deterministic (no ambient SynchronizationContext).
        Scheduler.Reset();
        var flush = new ScheduledFlush(TestSchedulerPump.Install());

        var renderer = new TestRenderer();
        var container = renderer.CreateContainer();

        var context = BuildContext(options, out var emitted);
        var rootVirtualNode = VirtualNodeFactory.Component(component, options.Properties, options.Slots);
        rootVirtualNode.AppContext = context;
        renderer.Render(rootVirtualNode, container);

        var instance = (ComponentInstance)rootVirtualNode.Component!;
        return new ComponentWrapper(instance, emitted, flush, renderer.Renderer, container, isRoot: true);
    }

    private static ApplicationContext BuildContext(ComponentMountOptions options, out EmittedEvents emitted)
    {
        var context = new ApplicationContext();
        foreach (var (key, value) in options.Provides)
        {
            context.Provides[key] = value;
        }
        foreach (var (name, definition) in options.Components)
        {
            context.Components[name] = definition;
        }
        if (options.Stubs.Count > 0)
        {
            var stubs = new Dictionary<IComponentDefinition, IComponentDefinition>();
            foreach (var (real, stub) in options.Stubs)
            {
                stubs[real] = stub ?? StubComponent.For(real);
            }
            context.ComponentStubs = stubs;
        }
        options.ConfigureApplication?.Invoke(context.Config);
        emitted = new EmittedEvents();
        context.EmitObserver = emitted.Record;
        return context;
    }
}
