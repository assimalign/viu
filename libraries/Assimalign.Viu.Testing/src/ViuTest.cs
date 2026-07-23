using System;
using System.Collections.Generic;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Testing;

/// <summary>Provides DOM-free mounting helpers over the in-memory test host.</summary>
public static class ViuTest
{
    /// <summary>Mounts an immutable component tree and returns its query wrapper.</summary>
    /// <param name="component">The primitive component tree.</param>
    /// <returns>The mounted wrapper.</returns>
    public static ComponentWrapper Mount(IComponent component)
    {
        return MountTree(component, options: null);
    }

    /// <summary>
    /// Mounts a component tree with application composition for template children, directives,
    /// services, and state.
    /// </summary>
    /// <param name="component">The component tree.</param>
    /// <param name="options">The application-level test mount options.</param>
    /// <returns>The mounted wrapper.</returns>
    public static ComponentWrapper Mount(
        IComponent component,
        ComponentMountOptions options)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(options);
        return MountTree(component, options);
    }

    private static ComponentWrapper MountTree(
        IComponent component,
        ComponentMountOptions? options)
    {
        EmittedEvents emitted = new();
        Dictionary<Type, ComponentActivator?> emptyStubs = [];
        TestComponentFactory components = new(
            options?.Components,
            options?.Stubs ?? emptyStubs);
        ApplicationContext application = new(
            component,
            components,
            options?.Services ?? EmptyServiceProvider.Instance,
            options?.State,
            options?.Directives);
        options?.ConfigureApplication?.Invoke(application);
        application.EventObserver = emitted.Record;

        Scheduler.Reset();
        ScheduledFlush flush = new(TestSchedulerPump.Install());
        try
        {
            TestRenderer renderer = new();
            TestElement container = renderer.CreateContainer();
            IComponentContext? context = renderer.Render(
                component,
                container,
                application);
            MountedTemplateNode<TestNode>? mountedTemplate =
                FindMountedTemplate(
                    renderer.Renderer,
                    container,
                    context);
            return new ComponentWrapper(
                component,
                mountedTemplate,
                emitted,
                flush,
                renderer.Renderer,
                container,
                ownsMount: true);
        }
        catch
        {
            flush.Dispose();
            Scheduler.Reset();
            throw;
        }
    }

    /// <summary>Mounts an authored component template and returns its query wrapper.</summary>
    /// <param name="template">The authored component template.</param>
    /// <param name="options">The arguments, slots, resolvers, and application configuration.</param>
    /// <returns>The mounted wrapper.</returns>
    public static ComponentWrapper Mount(
        IComponentTemplate template,
        ComponentMountOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        options ??= new ComponentMountOptions();

        EmittedEvents emitted = new();
        ITemplateComponent root = ComponentTree.Template(
            template.GetType(),
            options.Arguments,
            options.Slots,
            listeners: options.Listeners);
        TestComponentFactory components = new(
            template,
            options.Components,
            options.Stubs);
        ApplicationContext application = new(
            root,
            components,
            options.Services ?? EmptyServiceProvider.Instance,
            options.State,
            options.Directives);
        options.ConfigureApplication?.Invoke(application);
        application.EventObserver = emitted.Record;

        Scheduler.Reset();
        ScheduledFlush flush = new(TestSchedulerPump.Install());
        try
        {
            TestRenderer renderer = new();
            TestElement container = renderer.CreateContainer();
            IComponentContext? context = renderer.Render(
                root,
                container,
                application);
            if (context is null)
            {
                throw new InvalidOperationException(
                    "Core did not return a context for the mounted root template.");
            }

            MountedTemplateNode<TestNode> mountedTemplate =
                FindMountedTemplate(
                    renderer.Renderer,
                    container,
                    context)
                ?? throw new InvalidOperationException(
                    "Core did not expose the mounted root template.");
            return new ComponentWrapper(
                root,
                mountedTemplate,
                emitted,
                flush,
                renderer.Renderer,
                container,
                ownsMount: true);
        }
        catch
        {
            flush.Dispose();
            Scheduler.Reset();
            throw;
        }
    }

    private static MountedTemplateNode<TestNode>? FindMountedTemplate(
        Renderer<TestNode> renderer,
        TestElement container,
        IComponentContext? context)
    {
        if (context is null)
        {
            return null;
        }

        IReadOnlyList<MountedTemplateNode<TestNode>> templates =
            renderer.GetMountedTemplates(container);
        for (int index = 0; index < templates.Count; index++)
        {
            if (ReferenceEquals(templates[index].Instance.Context, context))
            {
                return templates[index];
            }
        }

        return null;
    }
}
