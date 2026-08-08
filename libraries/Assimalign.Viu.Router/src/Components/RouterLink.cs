using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Router;

/// <summary>
/// Renders a navigation anchor whose href and active classes come from the current router, and
/// intercepts only an unmodified primary-button click for client-side navigation.
/// </summary>
/// <remarks>
/// Modified, non-primary, already-prevented, and <c>target="_blank"</c> clicks remain native browser
/// navigations. The host-neutral click carrier keeps this library independent of Browser; the
/// Browser.Router leaf maps a live host event onto it. The router is resolved only through
/// <see cref="ComponentContext.Services"/>. The <c>to</c> input is a string path and the component
/// always emits an anchor; location-object targets and slot-only rendering are non-goals. Not
/// thread-safe; Viu drives it on the host event loop. Specified by <c>[RTR-4]</c>, <c>[RTR-7]</c>,
/// and <c>[CMP-33]</c>.
/// </remarks>
public sealed class RouterLink : IComponent
{
    private static readonly IReadOnlyDictionary<string, object?> EmptySlotArguments =
        new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(0, StringComparer.Ordinal));
    private static readonly ComponentContract Contract = new(
        renderCacheSize: 0,
        displayName: "RouterLink",
        flags: ComponentFlags.InheritFallthroughBindings,
        parameters:
        [
            new ComponentParameter("to", isRequired: true),
            new ComponentParameter("replace", defaultFactory: static () => false),
            new ComponentParameter("activeClass"),
            new ComponentParameter("exactActiveClass"),
        ]);

    /// <summary>Initializes one navigation-anchor component instance.</summary>
    public RouterLink()
    {
    }

    /// <summary>Gets the reflection-free component registration.</summary>
    public static ComponentRegistration Registration { get; } = new(
        ComponentReference.ForType(typeof(RouterLink)),
        Contract,
        static _ => new RouterLink());

    /// <inheritdoc/>
    public ComponentRenderer Setup(ComponentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Router? router = RouterResolution.Resolve(context);

        void Navigate(object? raw)
        {
            if (router is null)
            {
                return;
            }

            if (raw is RouterLinkClickEvent click)
            {
                if (click.HasSystemModifier || click.DefaultPrevented || click.Button != 0)
                {
                    return;
                }

                if (IsBlankTarget(context.Bindings.FallthroughBindings))
                {
                    return;
                }

                click.PreventDefault();
            }

            string? to = ReadString(context.Bindings.Parameters, "to");
            if (string.IsNullOrEmpty(to))
            {
                return;
            }

            Task<NavigationFailure?> navigation = ReadBoolean(
                context.Bindings.Parameters,
                "replace")
                    ? router.ReplaceAsync(to)
                    : router.PushAsync(to);
            ObserveNavigation(navigation);
        }

        return _ =>
        {
            IReadOnlyList<VirtualNode>? children = RenderDefaultSlot(context);
            if (router is null)
            {
                return new ElementNode(new QualifiedName("a"), children: children);
            }

            string to = ReadString(context.Bindings.Parameters, "to") ?? string.Empty;
            RouteLocation target = router.Resolve(to);
            RouteLocation current = router.CurrentRoute.Value;
            (bool isActive, bool isExactActive) = ComputeActive(current, target);

            string? activeClass =
                ReadString(context.Bindings.Parameters, "activeClass") ?? router.LinkActiveClass;
            string? exactActiveClass =
                ReadString(context.Bindings.Parameters, "exactActiveClass")
                ?? router.LinkExactActiveClass;
            string? classValue = BuildClass(
                isActive ? activeClass : null,
                isExactActive ? exactActiveClass : null);

            List<ElementBinding> anchorBindings =
            [
                ElementBinding.Attribute(
                    new QualifiedName("href"),
                    router.CreateHref(target)),
            ];
            if (classValue is not null)
            {
                anchorBindings.Add(
                    ElementBinding.Attribute(new QualifiedName("class"), classValue));
            }

            anchorBindings.Add(ElementBinding.Event("click", (Action<object?>)Navigate));
            return new ElementNode(
                new QualifiedName("a"),
                anchorBindings,
                children);
        };
    }

    private static IReadOnlyList<VirtualNode>? RenderDefaultSlot(ComponentContext context)
    {
        if (!context.Bindings.Slots.TryGetValue("default", out ComponentSlot? slot))
        {
            return null;
        }

        VirtualNode? content = slot(EmptySlotArguments);
        return content is null ? null : [content];
    }

    private static (bool IsActive, bool IsExactActive) ComputeActive(
        RouteLocation current,
        RouteLocation target)
    {
        if (target.Matched.Count == 0)
        {
            return (false, false);
        }

        RouteRecord targetLeaf = target.Matched[^1];
        int index = -1;
        for (int position = 0; position < current.Matched.Count; position++)
        {
            if (ReferenceEquals(current.Matched[position], targetLeaf))
            {
                index = position;
                break;
            }
        }

        if (index < 0)
        {
            return (false, false);
        }

        bool isActive = IncludesParameters(current.Parameters, target.Parameters);
        bool isExactActive = isActive
            && index == current.Matched.Count - 1
            && current.Parameters.Equals(target.Parameters);
        return (isActive, isExactActive);
    }

    private static bool IncludesParameters(RouteParameters current, RouteParameters target)
    {
        foreach (string name in target.Names)
        {
            if (!current.TryGetString(name, out string? currentValue)
                || !string.Equals(
                    currentValue,
                    target.GetString(name),
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static string? BuildClass(string? active, string? exactActive)
    {
        if (active is null)
        {
            return exactActive;
        }

        return exactActive is null ? active : active + " " + exactActive;
    }

    private static bool IsBlankTarget(
        IReadOnlyDictionary<string, object?> fallthroughBindings)
    {
        if (!fallthroughBindings.TryGetValue("target", out object? value))
        {
            return false;
        }

        string? target = value switch
        {
            string text => text,
            ElementBinding binding => binding.Value as string,
            _ => null,
        };
        return target?.Contains("_blank", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string? ReadString(
        IReadOnlyDictionary<string, object?> parameters,
        string name)
    {
        return parameters.TryGetValue(name, out object? value) ? value as string : null;
    }

    private static bool ReadBoolean(
        IReadOnlyDictionary<string, object?> parameters,
        string name)
    {
        return parameters.TryGetValue(name, out object? value) && value is true;
    }

    private static void ObserveNavigation(Task<NavigationFailure?> navigation)
    {
        _ = navigation.ContinueWith(
            static task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted
                | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
