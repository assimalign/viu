using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu;

/// <summary>
/// Owns one activated template, its live context, reactive scope, renderer, and lifecycle.
/// </summary>
internal sealed class MountedComponent : IDisposable
{
    private readonly IComponentTemplate _template;
    private readonly ComponentRenderer _renderer;
    private bool _isDisposed;

    private MountedComponent(
        IComponentTemplate template,
        ComponentContext context,
        ComponentRenderer renderer)
    {
        _template = template;
        Context = context;
        _renderer = renderer;
    }

    internal ComponentContext Context { get; }

    internal IComponentTemplate Template => _template;

    internal static MountedComponent Create(
        IApplicationContext application,
        ITemplateComponent request,
        ComponentContext? parent = null,
        int identifier = 0)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(request);

        IComponentTemplate template = CreateTemplate(application, request);

        EffectScope scope = parent is null
            ? new EffectScope(detached: true)
            : parent.Scope.Run(static () => new EffectScope());
        ComponentContext context = new(
            application,
            template,
            request,
            scope,
            parent,
            identifier);

        try
        {
            ComponentRenderer renderer = scope.Run(
                () => context.Run(() => template.Setup(context)));
            if (renderer is null)
            {
                throw new InvalidOperationException(
                    "Component setup returned a null renderer.");
            }

            return new MountedComponent(template, context, renderer);
        }
        catch
        {
            scope.Stop();
            context.Lifecycle.Dispose();
            if (template is IDisposable disposable)
            {
                disposable.Dispose();
            }

            throw;
        }
    }

    private static IComponentTemplate CreateTemplate(
        IApplicationContext application,
        ITemplateComponent request)
    {
        Type? templateType = request.TemplateType;
        if (templateType == typeof(BaseTransition))
        {
            return BaseTransition.Registration.Activator();
        }

        if (templateType == typeof(KeepAlive))
        {
            return KeepAlive.Registration.Activator();
        }

        if (templateType == typeof(Suspense))
        {
            return Suspense.Registration.Activator();
        }

        string? templateName = request.TemplateName;
        if (string.Equals(
            templateName,
            BaseTransition.Registration.Name,
            StringComparison.Ordinal))
        {
            return BaseTransition.Registration.Activator();
        }

        if (string.Equals(
            templateName,
            KeepAlive.Registration.Name,
            StringComparison.Ordinal))
        {
            return KeepAlive.Registration.Activator();
        }

        if (string.Equals(
            templateName,
            Suspense.Registration.Name,
            StringComparison.Ordinal))
        {
            return Suspense.Registration.Activator();
        }

        return templateType is not null
            ? application.Components.Create(templateType)
            : application.Components.Create(templateName!);
    }

    internal IComponent Render()
    {
        try
        {
            IComponent? component = Context.Scope.Run(
                () => Context.Run(() => _renderer()));
            return ApplyRootBehavior(component ?? ComponentTree.Comment());
        }
        catch (Exception exception)
        {
            RenderHelpers.ClearBlockTrackingAfterRenderFailure();
            ComponentErrorHandling.Handle(exception, Context, "component render");
            return ComponentTree.Comment();
        }
    }

    private IComponent ApplyRootBehavior(IComponent component)
    {
        if (_template is IComponentRootBehaviorForwarder)
        {
            return component;
        }

        if (component is IElementComponent element)
        {
            IComponentAttributeCollection attributes =
                (_template.Flags & ComponentFlags.InheritAttributes) != 0
                && Context.Attributes.Count > 0
                    ? MergeAttributes(element.Attributes, Context.Attributes)
                    : element.Attributes;
            IReadOnlyList<IComponentDirectiveBinding> directives =
                Context.RootDirectives.Count > 0
                    ? MergeDirectives(element.Directives, Context.RootDirectives)
                    : element.Directives;
            if (!ReferenceEquals(attributes, element.Attributes)
                || !ReferenceEquals(directives, element.Directives))
            {
                return ComponentTree.Element(
                    element.Tag,
                    attributes,
                    element.Children,
                    element.Key,
                    element.Optimization,
                    directives,
                    element.Reference);
            }

            return component;
        }

        if ((_template.Flags & ComponentFlags.InheritAttributes) != 0
            && Context.Attributes.Count > 0)
        {
            Context.Application.WarnHandler?.Invoke(
                "Extraneous fallthrough attributes could not be applied because the component "
                + "does not render a single element root.");
        }

        if (Context.RootDirectives.Count > 0)
        {
            Context.Application.WarnHandler?.Invoke(
                "Runtime directives on a component require a single element root.");
        }

        return component;
    }

    private static IComponentAttributeCollection MergeAttributes(
        IComponentAttributeCollection root,
        IComponentAttributeCollection fallthrough)
    {
        IReadOnlyDictionary<string, object?> mergedProperties =
            RenderHelpers._mergeProps(root, fallthrough);
        List<IComponentAttribute> merged = new(mergedProperties.Count);
        foreach (KeyValuePair<string, object?> property in mergedProperties)
        {
            merged.Add(new ComponentAttribute(property.Key, property.Value));
        }

        return new ComponentAttributes(merged);
    }

    private static IReadOnlyList<IComponentDirectiveBinding> MergeDirectives(
        IReadOnlyList<IComponentDirectiveBinding> root,
        IReadOnlyList<IComponentDirectiveBinding> inherited)
    {
        List<IComponentDirectiveBinding> merged =
            new(root.Count + inherited.Count);
        for (int index = 0; index < root.Count; index++)
        {
            merged.Add(root[index]);
        }

        for (int index = 0; index < inherited.Count; index++)
        {
            merged.Add(inherited[index]);
        }

        return merged.AsReadOnly();
    }

    internal ReactiveEffect CreateRenderEffect(Action render, Action scheduler)
    {
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(scheduler);
        return Context.Scope.Run(
            () => new ReactiveEffect(() => Context.Run(render))
            {
                Scheduler = scheduler,
            });
    }

    internal void Update(ITemplateComponent request)
    {
        Context.Update(request);
    }

    internal void UpdateRequest(ITemplateComponent request)
    {
        Context.UpdateRequest(request);
    }

    internal void InvokeBeforeMount() => Context.Lifecycle.InvokeBeforeMount(Context);

    internal void InvokeMounted() => Context.Lifecycle.InvokeMounted(Context);

    internal void InvokeBeforeUpdate() => Context.Lifecycle.InvokeBeforeUpdate(Context);

    internal void InvokeUpdated() => Context.Lifecycle.InvokeUpdated(Context);

    internal void InvokeActivated() => Context.Lifecycle.InvokeActivated(Context);

    internal void InvokeDeactivated() => Context.Lifecycle.InvokeDeactivated(Context);

    internal Task InvokeServerPrefetchAsync() =>
        Context.Lifecycle.InvokeServerPrefetchAsync(Context);

    internal void Unmount(Action unmountSubtree)
    {
        ArgumentNullException.ThrowIfNull(unmountSubtree);
        if (_isDisposed)
        {
            return;
        }

        ExceptionDispatchInfo? error = null;
        try
        {
            Context.Lifecycle.InvokeBeforeUnmount(Context);
        }
        catch (Exception exception)
        {
            error = ExceptionDispatchInfo.Capture(exception);
        }

        Context.Lifecycle.Cancel();
        try
        {
            Context.Scope.Stop();
        }
        catch (Exception exception)
        {
            error ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            unmountSubtree();
        }
        catch (Exception exception)
        {
            error ??= ExceptionDispatchInfo.Capture(exception);
        }

        Context.IsUnmounted = true;
        try
        {
            Context.Lifecycle.InvokeUnmounted(Context);
        }
        catch (Exception exception)
        {
            error ??= ExceptionDispatchInfo.Capture(exception);
        }

        Dispose();
        error?.Throw();
    }

    internal void AbortMount(Action? unmountSubtree = null)
    {
        if (_isDisposed)
        {
            return;
        }

        Context.Lifecycle.Cancel();
        Context.Scope.Stop();
        unmountSubtree?.Invoke();
        Context.IsUnmounted = true;
        Dispose();
    }

    /// <summary>Disposes the mount-owned template and lifecycle resources.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Context.Lifecycle.Dispose();
        if (_template is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
