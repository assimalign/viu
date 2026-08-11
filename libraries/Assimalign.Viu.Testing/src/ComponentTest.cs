using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Testing;

/// <summary>Provides DOM-free component and virtual-tree mount entry points.</summary>
/// <remarks>
/// Mounts own a deterministic synchronization context and scheduler lease, and use only public
/// application, renderer, mounted-view, and event-observer seams. Specified by <c>[APP-2]</c>,
/// <c>[RND-6]</c>, seam S2, seam S3, seam S5, <c>[CONF-3]</c>, and
/// <c>[V01.01.11.05]</c>.
/// </remarks>
public static class ComponentTest
{
    /// <summary>Mounts an immutable virtual tree with default application composition.</summary>
    /// <param name="node">The root tree.</param>
    /// <returns>The owning query wrapper.</returns>
    public static ComponentWrapper Mount(VirtualNode node) => Mount(node, options: null);

    /// <summary>Mounts an immutable virtual tree with explicit application composition.</summary>
    /// <param name="node">The root tree.</param>
    /// <param name="options">Application composition, or null for defaults.</param>
    /// <returns>The owning query wrapper.</returns>
    public static ComponentWrapper Mount(
        VirtualNode node,
        ComponentMountOptions? options)
    {
        ArgumentNullException.ThrowIfNull(node);
        return MountCore(node, rootRegistration: null, options);
    }

    /// <summary>Mounts the exact supplied authored component instance once with default composition.</summary>
    /// <param name="component">The caller-supplied root instance.</param>
    /// <returns>The owning query wrapper.</returns>
    public static ComponentWrapper Mount(IComponent component) => Mount(component, options: null);

    /// <summary>Mounts the exact supplied authored component instance once with explicit composition.</summary>
    /// <param name="component">The caller-supplied root instance.</param>
    /// <param name="options">Invocation and application composition, or null for defaults.</param>
    /// <returns>The owning query wrapper.</returns>
    public static ComponentWrapper Mount(
        IComponent component,
        ComponentMountOptions? options)
    {
        ArgumentNullException.ThrowIfNull(component);
        options ??= new ComponentMountOptions();
        ComponentReference reference = ComponentReference.ForType(component.GetType());
        ComponentRegistration registration = new(
            reference,
            options.RootContract
                ?? new ComponentContract(displayName: component.GetType().Name),
            _ => component);
        return MountRegistration(registration, options);
    }

    /// <summary>Mounts a root through its reflection-free registration with default composition.</summary>
    /// <param name="registration">The root reference, contract, and activator.</param>
    /// <returns>The owning query wrapper.</returns>
    public static ComponentWrapper Mount(ComponentRegistration registration) =>
        Mount(registration, options: null);

    /// <summary>Mounts a root through its reflection-free registration with explicit composition.</summary>
    /// <param name="registration">The root reference, contract, and activator.</param>
    /// <param name="options">Invocation and application composition, or null for defaults.</param>
    /// <returns>The owning query wrapper.</returns>
    public static ComponentWrapper Mount(
        ComponentRegistration registration,
        ComponentMountOptions? options)
    {
        ArgumentNullException.ThrowIfNull(registration);
        options ??= new ComponentMountOptions();
        return MountRegistration(registration, options);
    }

    private static ComponentWrapper MountRegistration(
        ComponentRegistration registration,
        ComponentMountOptions options)
    {
        ComponentInvocation invocation = new(
            options.Arguments,
            options.Slots,
            options.Listeners,
            options.RootDirectives,
            options.SlotStability);
        ComponentNode root = new(registration.Reference, invocation);
        return MountCore(root, registration, options);
    }

    private static ComponentWrapper MountCore(
        VirtualNode root,
        ComponentRegistration? rootRegistration,
        ComponentMountOptions? options)
    {
        options ??= new ComponentMountOptions();
        EmittedEvents emitted = new();
        ApplicationOptions applicationOptions = new();
        options.ConfigureApplication?.Invoke(applicationOptions);

        IComponentFactory descendants = options.Components ?? applicationOptions.Components;
        applicationOptions.Components = new TestComponentFactory(
            rootRegistration,
            descendants,
            options.Stubs);
        applicationOptions.RootComponent = root;
        if (options.Services is not null)
        {
            applicationOptions.Services = options.Services;
        }

        if (options.State is not null)
        {
            applicationOptions.State = options.State;
        }

        if (options.Directives is not null)
        {
            applicationOptions.Directives = options.Directives;
        }

        Action<ComponentContext, string, IReadOnlyList<object?>>? configuredObserver =
            applicationOptions.EventObserver;
        applicationOptions.EventObserver = (context, name, arguments) =>
        {
            emitted.Record(context, name, arguments);
            configuredObserver?.Invoke(context, name, arguments);
        };
        ApplicationContext application = new(applicationOptions);

        Scheduler.Reset();
        TestRenderer testRenderer = new();
        ScheduledFlush flush = new(
            TestSchedulerPump.Install(testRenderer.SynchronizationContext),
            testRenderer);
        try
        {
            TestElement container = testRenderer.CreateContainer();
            testRenderer.Render(root, container, application);
            MountedComponentView<TestNode>? mountedView =
                FindRootView(testRenderer.Renderer, container, root);
            if (root is ComponentNode && mountedView is null)
            {
                throw new InvalidOperationException(
                    "Core did not expose the mounted root component view.");
            }

            return new ComponentWrapper(
                root,
                mountedView,
                emitted,
                flush,
                testRenderer,
                container,
                ownsMount: true);
        }
        catch (Exception exception)
        {
            Scheduler.Reset();
            try
            {
                flush.Dispose();
            }
            catch
            {
                // Preserve the mount failure; the renderer never escapes to own pending work.
            }

            ExceptionDispatchInfo.Capture(exception).Throw();
            throw;
        }
    }

    private static MountedComponentView<TestNode>? FindRootView(
        Renderer<TestNode> renderer,
        TestElement container,
        VirtualNode root)
    {
        IReadOnlyList<MountedComponentView<TestNode>> views =
            renderer.GetMountedComponentViews(container);
        for (int index = 0; index < views.Count; index++)
        {
            if (ReferenceEquals(views[index].Request, root))
            {
                return views[index];
            }
        }

        return null;
    }
}
