using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Shared;

namespace Assimalign.Viu;

/// <summary>
/// Implements the by-name runtime-helper contract emitted by the Viu template compiler and creates
/// values in the unified <see cref="IComponent"/> tree.
/// </summary>
/// <remarks>
/// Mirrors Vue 3.5's runtime-core render helpers:
/// https://github.com/vuejs/core/tree/v3.5.29/packages/runtime-core/src.
/// The underscore-prefixed names deliberately match the compiler's generated-code contract and are
/// therefore a narrow exception to the repository naming rules. Deviates from the repository
/// whole-word naming rule per design decision: generated render bodies bind these helper names
/// literally. This type is not thread-safe; Viu's runtime executes on the single-threaded host event
/// loop.
/// </remarks>
public static class RenderHelpers
{
    private static readonly List<BlockFrame> BlockFrames = new();
    private static ConditionalWeakTable<IComponent, MemoMetadata> _memoMetadata = new();
    private static int _blockTrackingDepth = 1;

    /// <summary>Gets the compiler marker for a fragment tree value.</summary>
    public static readonly object _Fragment = new BuiltInComponentType("Fragment");

    /// <summary>Gets the compiler marker for a teleport tree value.</summary>
    public static readonly object _Teleport = new BuiltInComponentType("Teleport");

    /// <summary>Gets the registered template type for the host-neutral Suspense built-in.</summary>
    public static readonly object _Suspense = typeof(Suspense);

    /// <summary>Gets the registered template type for the host-neutral KeepAlive built-in.</summary>
    public static readonly object _KeepAlive = typeof(KeepAlive);

    /// <summary>Gets the registered template type for the host-neutral BaseTransition built-in.</summary>
    public static readonly object _BaseTransition = typeof(BaseTransition);

    /// <summary>
    /// Opens an optimization block and returns the token used to preserve generated evaluation order.
    /// </summary>
    /// <param name="disableTracking">
    /// Whether descendants should be excluded from the block's dynamic-child collection.
    /// </param>
    /// <returns>An opaque block token.</returns>
    public static BlockToken _openBlock(bool disableTracking = false)
    {
        BlockFrames.Add(new BlockFrame(disableTracking));
        return new BlockToken(0);
    }

    /// <summary>Suspends or resumes block-tree collection.</summary>
    /// <param name="value">A negative value suspends tracking; a positive value resumes it.</param>
    /// <param name="inVOnce">Whether the suspension begins a <c>v-once</c> subtree.</param>
    /// <returns>A token used by <see cref="_setCache"/> to apply the inverse change.</returns>
    public static BlockToken _setBlockTracking(int value, bool inVOnce = false)
    {
        _blockTrackingDepth += value;
        if (inVOnce && value < 0 && BlockFrames.Count > 0)
        {
            BlockFrames[^1].HasOnce = true;
        }

        return new BlockToken(value);
    }

    /// <summary>Creates an element or fragment block.</summary>
    /// <param name="block">The token returned by <see cref="_openBlock"/>.</param>
    /// <param name="tag">An element tag or <see cref="_Fragment"/>.</param>
    /// <param name="properties">The generated property bag.</param>
    /// <param name="children">The generated child value or values.</param>
    /// <param name="patchFlag">The compiler patch flags.</param>
    /// <param name="dynamicProperties">The selectively patchable property names.</param>
    /// <returns>The block root.</returns>
    public static IComponent _createElementBlock(
        BlockToken block,
        object? tag,
        object? properties = null,
        object? children = null,
        int patchFlag = 0,
        string[]? dynamicProperties = null)
    {
        _ = block;
        return CreateComponent(
            tag,
            properties,
            children,
            (PatchFlags)patchFlag,
            dynamicProperties,
            asBlock: true);
    }

    /// <summary>Creates a template, dynamic, or built-in block.</summary>
    /// <param name="block">The token returned by <see cref="_openBlock"/>.</param>
    /// <param name="tag">A template type, resolved name, element tag, or built-in marker.</param>
    /// <param name="properties">The generated property bag.</param>
    /// <param name="children">Children or component slots.</param>
    /// <param name="patchFlag">The compiler patch flags.</param>
    /// <param name="dynamicProperties">The selectively patchable property names.</param>
    /// <returns>The block root.</returns>
    public static IComponent _createBlock(
        BlockToken block,
        object? tag,
        object? properties = null,
        object? children = null,
        int patchFlag = 0,
        string[]? dynamicProperties = null)
    {
        _ = block;
        return CreateComponent(
            tag,
            properties,
            children,
            (PatchFlags)patchFlag,
            dynamicProperties,
            asBlock: true);
    }

    /// <summary>Creates a non-block element or fragment.</summary>
    /// <param name="tag">An element tag or <see cref="_Fragment"/>.</param>
    /// <param name="properties">The generated property bag.</param>
    /// <param name="children">The generated child value or values.</param>
    /// <param name="patchFlag">The compiler patch flags.</param>
    /// <param name="dynamicProperties">The selectively patchable property names.</param>
    /// <returns>The tree value.</returns>
    public static IComponent _createElementVNode(
        object? tag,
        object? properties = null,
        object? children = null,
        int patchFlag = 0,
        string[]? dynamicProperties = null)
    {
        return CreateComponent(
            tag,
            properties,
            children,
            (PatchFlags)patchFlag,
            dynamicProperties,
            asBlock: false);
    }

    /// <summary>Creates a non-block template, dynamic, or element value.</summary>
    /// <param name="tag">A template type, resolved name, element tag, or built-in marker.</param>
    /// <param name="properties">The generated property bag.</param>
    /// <param name="children">Children or component slots.</param>
    /// <param name="patchFlag">The compiler patch flags.</param>
    /// <param name="dynamicProperties">The selectively patchable property names.</param>
    /// <returns>The tree value.</returns>
    public static IComponent _createVNode(
        object? tag,
        object? properties = null,
        object? children = null,
        int patchFlag = 0,
        string[]? dynamicProperties = null)
    {
        return CreateComponent(
            tag,
            properties,
            children,
            (PatchFlags)patchFlag,
            dynamicProperties,
            asBlock: false);
    }

    /// <summary>Creates a text tree value.</summary>
    /// <param name="text">The text or value to display.</param>
    /// <param name="patchFlag">The compiler patch flags.</param>
    /// <returns>The text value.</returns>
    public static ITextComponent _createTextVNode(object? text = null, int patchFlag = 0)
    {
        ITextComponent component = ComponentTree.Text(
            CoerceText(text),
            new ComponentOptimization((PatchFlags)patchFlag));
        TrackDynamicComponent(component);
        return component;
    }

    /// <summary>Creates a comment or empty-render placeholder.</summary>
    /// <param name="text">The comment text.</param>
    /// <param name="asBlock">Whether to create a block-form comment.</param>
    /// <returns>The comment value.</returns>
    public static ICommentComponent _createCommentVNode(string? text = "", bool asBlock = false)
    {
        if (!asBlock)
        {
            return ComponentTree.Comment(text ?? string.Empty);
        }

        _openBlock();
        return CompleteBlock(
            optimization => new RuntimeCommentComponent(text ?? string.Empty, optimization),
            default,
            null);
    }

    /// <summary>Creates a platform-specific static-content tree value.</summary>
    /// <param name="content">The raw static content.</param>
    /// <param name="count">The compiler's top-level-node count hint.</param>
    /// <returns>The static tree value.</returns>
    public static IStaticComponent _createStaticVNode(string content, int count)
    {
        _ = count;
        return ComponentTree.Static(content);
    }

    /// <summary>Formats an interpolation value for display.</summary>
    /// <param name="value">The value to format.</param>
    /// <returns>The display string.</returns>
    public static string _toDisplayString(object? value)
    {
        return DisplayStringFormatter.ToDisplayString(value);
    }

    /// <summary>Renders each item in an enumerable.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <typeparam name="TResult">The generated result type.</typeparam>
    /// <param name="source">The source values.</param>
    /// <param name="render">The per-item renderer.</param>
    /// <returns>The rendered values.</returns>
    public static TResult[] _renderList<T, TResult>(
        IEnumerable<T>? source,
        Func<T, TResult> render)
    {
        ArgumentNullException.ThrowIfNull(render);
        if (source is null)
        {
            return Array.Empty<TResult>();
        }

        List<TResult> result =
            new(source is ICollection<T> collection ? collection.Count : 4);
        foreach (T item in source)
        {
            result.Add(render(item));
        }

        return result.ToArray();
    }

    /// <summary>Renders each item and its zero-based index.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <typeparam name="TResult">The generated result type.</typeparam>
    /// <param name="source">The source values.</param>
    /// <param name="render">The per-item renderer.</param>
    /// <returns>The rendered values.</returns>
    public static TResult[] _renderList<T, TResult>(
        IEnumerable<T>? source,
        Func<T, int, TResult> render)
    {
        ArgumentNullException.ThrowIfNull(render);
        if (source is null)
        {
            return Array.Empty<TResult>();
        }

        List<TResult> result =
            new(source is ICollection<T> collection ? collection.Count : 4);
        int index = 0;
        foreach (T item in source)
        {
            result.Add(render(item, index));
            index++;
        }

        return result.ToArray();
    }

    /// <summary>Renders a one-based numeric range.</summary>
    /// <typeparam name="TResult">The generated result type.</typeparam>
    /// <param name="count">The inclusive upper bound.</param>
    /// <param name="render">The per-number renderer.</param>
    /// <returns>The rendered values.</returns>
    public static TResult[] _renderList<TResult>(int count, Func<int, TResult> render)
    {
        ArgumentNullException.ThrowIfNull(render);
        TResult[] result = new TResult[Math.Max(0, count)];
        for (int index = 0; index < result.Length; index++)
        {
            result[index] = render(index + 1);
        }

        return result;
    }

    /// <summary>Renders a one-based numeric range with a zero-based index.</summary>
    /// <typeparam name="TResult">The generated result type.</typeparam>
    /// <param name="count">The inclusive upper bound.</param>
    /// <param name="render">The per-number renderer.</param>
    /// <returns>The rendered values.</returns>
    public static TResult[] _renderList<TResult>(
        int count,
        Func<int, int, TResult> render)
    {
        ArgumentNullException.ThrowIfNull(render);
        TResult[] result = new TResult[Math.Max(0, count)];
        for (int index = 0; index < result.Length; index++)
        {
            result[index] = render(index + 1, index);
        }

        return result;
    }

    /// <summary>Renders key/value entries with their zero-based index.</summary>
    /// <typeparam name="TKey">The entry key type.</typeparam>
    /// <typeparam name="TValue">The entry value type.</typeparam>
    /// <typeparam name="TResult">The generated result type.</typeparam>
    /// <param name="source">The entry source.</param>
    /// <param name="render">The per-entry renderer.</param>
    /// <returns>The rendered values.</returns>
    public static TResult[] _renderList<TKey, TValue, TResult>(
        IEnumerable<KeyValuePair<TKey, TValue>>? source,
        Func<TValue, TKey, int, TResult> render)
    {
        ArgumentNullException.ThrowIfNull(render);
        if (source is null)
        {
            return Array.Empty<TResult>();
        }

        List<TResult> result =
            new(source is ICollection<KeyValuePair<TKey, TValue>> collection
                ? collection.Count
                : 4);
        int index = 0;
        foreach (KeyValuePair<TKey, TValue> item in source)
        {
            result.Add(render(item.Value, item.Key, index));
            index++;
        }

        return result.ToArray();
    }

    /// <summary>Wraps an unscoped generated slot function.</summary>
    /// <param name="render">The generated slot renderer.</param>
    /// <returns>The component-slot delegate.</returns>
    public static ComponentSlot _withCtx(Func<object?[]?> render)
    {
        ArgumentNullException.ThrowIfNull(render);
        return _ => NormalizeSlotResult(render());
    }

    /// <summary>Wraps a scoped generated slot function.</summary>
    /// <param name="render">The generated slot renderer.</param>
    /// <returns>The component-slot delegate.</returns>
    public static ComponentSlot _withCtx(Func<object?, object?[]?> render)
    {
        ArgumentNullException.ThrowIfNull(render);
        return arguments => NormalizeSlotResult(render(arguments));
    }

    /// <summary>Renders a named slot or its fallback content.</summary>
    /// <param name="slots">The current component's slots.</param>
    /// <param name="name">The slot name.</param>
    /// <param name="properties">The scoped-slot arguments.</param>
    /// <param name="fallback">The optional fallback renderer.</param>
    /// <returns>The rendered slot subtree.</returns>
    public static IComponent _renderSlot(
        IReadOnlyDictionary<string, ComponentSlot>? slots,
        string name,
        object? properties = null,
        Func<object?[]?>? fallback = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (slots is not null && slots.TryGetValue(name, out ComponentSlot? slot))
        {
            IComponent? result = slot(BuildArguments(properties));
            if (HasRenderableSlotContent(result))
            {
                return result!;
            }
        }

        return fallback is null
            ? ComponentTree.Comment()
            : NormalizeSlotResult(fallback()) ?? ComponentTree.Comment();
    }

    /// <summary>Merges static and compiler-produced dynamic slots.</summary>
    /// <param name="slots">The statically named slot property bag.</param>
    /// <param name="dynamicSlots">Dynamic slot descriptors and rendered descriptor arrays.</param>
    /// <returns>A merged slot property bag.</returns>
    public static IReadOnlyDictionary<string, object?> _createSlots(
        object? slots,
        object?[] dynamicSlots)
    {
        ArgumentNullException.ThrowIfNull(dynamicSlots);
        Dictionary<string, object?> merged = CopyProperties(slots);
        foreach (object? dynamicSlot in dynamicSlots)
        {
            MergeDynamicSlot(merged, dynamicSlot);
        }

        return new ReadOnlyDictionary<string, object?>(merged);
    }

    /// <summary>
    /// Creates a deferred named-template reference without resolving or activating a template.
    /// </summary>
    /// <param name="name">The registered template name.</param>
    /// <returns>An opaque name reference consumed by the tree factories.</returns>
    public static object _resolveComponent(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return new NamedTemplateType(name);
    }

    /// <summary>Creates a deferred directive-name reference.</summary>
    /// <param name="name">The registered directive name.</param>
    /// <returns>An opaque directive reference.</returns>
    public static object _resolveDirective(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return new DirectiveReference(name);
    }

    /// <summary>
    /// Preserves a dynamic component selector until the tree request is created.
    /// </summary>
    /// <param name="value">
    /// A template type, explicit <see cref="DynamicComponentName"/>, resolved name reference,
    /// element name, asynchronous definition, or null. Plain strings remain element tags because
    /// the application component factory intentionally has no probing API.
    /// </param>
    /// <returns>The unchanged selector.</returns>
    public static object? _resolveDynamicComponent(object? value)
    {
        return DynamicComponents.ResolveDynamicComponent(value);
    }

    /// <summary>Attaches generated directive metadata to an element or template request.</summary>
    /// <param name="component">The component tree value.</param>
    /// <param name="directives">The generated directive tuples.</param>
    /// <returns>A copy carrying the directive bindings.</returns>
    public static IComponent _withDirectives(IComponent component, object?[] directives)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(directives);

        List<IComponentDirectiveBinding> bindings = new();
        switch (component)
        {
            case IElementComponent element:
                bindings.AddRange(element.Directives);
                break;
            case ITemplateComponent template:
                bindings.AddRange(template.Directives);
                break;
        }

        foreach (object? entry in directives)
        {
            if (entry is not object?[] tuple || tuple.Length == 0 || tuple[0] is null)
            {
                continue;
            }

            bindings.Add(CreateDirectiveBinding(tuple));
        }

        IComponent result = component switch
        {
            IElementComponent element => new ElementComponent(
                element.Tag,
                element.Attributes,
                element.Children,
                element.Key,
                element.Optimization,
                bindings,
                element.Reference),
            ITemplateComponent { TemplateType: Type templateType } template => new TemplateComponent(
                templateType,
                template.Arguments,
                template.Slots,
                template.Key,
                template.Optimization,
                template.Listeners,
                bindings,
                template.Reference),
            ITemplateComponent { TemplateName: string templateName } template => new TemplateComponent(
                templateName,
                template.Arguments,
                template.Slots,
                template.Key,
                template.Optimization,
                template.Listeners,
                bindings,
                template.Reference),
            _ => throw new NotSupportedException(
                $"Directives cannot be attached to component kind '{component.Kind}'."),
        };

        ReplaceTrackedComponent(component, result);
        return result;
    }

    /// <summary>Merges generated property sources using Vue-compatible class, style, and event rules.</summary>
    /// <param name="sources">The property sources.</param>
    /// <returns>The merged property bag.</returns>
    public static IReadOnlyDictionary<string, object?> _mergeProps(params object?[] sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        Dictionary<string, object?> merged = new(StringComparer.Ordinal);
        foreach (object? source in sources)
        {
            foreach (KeyValuePair<string, object?> property in ReadProperties(source))
            {
                if (string.Equals(property.Key, "class", StringComparison.Ordinal)
                    && merged.TryGetValue(property.Key, out object? existingClass))
                {
                    merged[property.Key] =
                        StyleAndClassNormalization.NormalizeClass(
                            new object?[] { existingClass, property.Value });
                }
                else if (string.Equals(property.Key, "style", StringComparison.Ordinal)
                    && merged.TryGetValue(property.Key, out object? existingStyle))
                {
                    merged[property.Key] =
                        StyleAndClassNormalization.NormalizeStyle(
                            new object?[] { existingStyle, property.Value });
                }
                else if (IsEventListenerName(property.Key)
                    && merged.TryGetValue(property.Key, out object? existingHandler)
                    && existingHandler is Delegate existingDelegate
                    && property.Value is Delegate incomingDelegate
                    && existingDelegate.GetType() == incomingDelegate.GetType()
                    && !ReferenceEquals(existingDelegate, incomingDelegate))
                {
                    merged[property.Key] = Delegate.Combine(existingDelegate, incomingDelegate);
                }
                else
                {
                    merged[property.Key] = property.Value;
                }
            }
        }

        return new ReadOnlyDictionary<string, object?>(merged);
    }

    /// <summary>Normalizes a dynamic class binding.</summary>
    /// <param name="value">The class binding.</param>
    /// <returns>The normalized class string.</returns>
    public static string _normalizeClass(object? value)
    {
        return StyleAndClassNormalization.NormalizeClass(value);
    }

    /// <summary>Normalizes a dynamic style binding.</summary>
    /// <param name="value">The style binding.</param>
    /// <returns>The normalized style representation.</returns>
    public static object? _normalizeStyle(object? value)
    {
        return StyleAndClassNormalization.NormalizeStyle(value);
    }

    /// <summary>Normalizes the class and style entries in a generated property bag.</summary>
    /// <param name="properties">The property source.</param>
    /// <returns>A normalized snapshot of the source, or the original non-property value.</returns>
    public static object? _normalizeProps(object? properties)
    {
        if (!CanReadProperties(properties))
        {
            return properties;
        }

        Dictionary<string, object?> normalized = CopyProperties(properties);
        if (normalized.TryGetValue("class", out object? cssClass))
        {
            normalized["class"] = StyleAndClassNormalization.NormalizeClass(cssClass);
        }

        if (normalized.TryGetValue("style", out object? style))
        {
            normalized["style"] = StyleAndClassNormalization.NormalizeStyle(style);
        }

        return new ReadOnlyDictionary<string, object?>(normalized);
    }

    /// <summary>Returns a property source unchanged because Viu does not use identity-swapping proxies.</summary>
    /// <param name="properties">The property source.</param>
    /// <returns><paramref name="properties"/>.</returns>
    public static object? _guardReactiveProps(object? properties)
    {
        return properties;
    }

    /// <summary>Prefixes a generated event map's keys with <c>on</c>.</summary>
    /// <param name="value">The unprefixed event property source.</param>
    /// <returns>The prefixed event property bag.</returns>
    public static IReadOnlyDictionary<string, object?> _toHandlers(object? value)
    {
        Dictionary<string, object?> handlers = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> handler in ReadProperties(value))
        {
            handlers[_toHandlerKey(handler.Key)] = handler.Value;
        }

        return new ReadOnlyDictionary<string, object?>(handlers);
    }

    /// <summary>Converts a hyphenated name to camel case.</summary>
    /// <param name="value">The input name.</param>
    /// <returns>The camel-cased name.</returns>
    public static string _camelize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.IndexOf('-', StringComparison.Ordinal) < 0)
        {
            return value;
        }

        char[] buffer = new char[value.Length];
        int length = 0;
        bool capitalizeNext = false;
        foreach (char character in value)
        {
            if (character == '-')
            {
                capitalizeNext = true;
                continue;
            }

            buffer[length] = capitalizeNext
                ? char.ToUpperInvariant(character)
                : character;
            length++;
            capitalizeNext = false;
        }

        return new string(buffer, 0, length);
    }

    /// <summary>Capitalizes the first character of a string.</summary>
    /// <param name="value">The input string.</param>
    /// <returns>The capitalized string.</returns>
    public static string _capitalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Length == 0
            ? value
            : char.ToUpperInvariant(value[0]) + value[1..];
    }

    /// <summary>Builds an <c>onEvent</c> property name.</summary>
    /// <param name="value">The event name.</param>
    /// <returns>The handler property name.</returns>
    public static string _toHandlerKey(object? value)
    {
        string name = value as string ?? DisplayStringFormatter.ToDisplayString(value);
        return name.Length == 0 ? string.Empty : "on" + _capitalize(_camelize(name));
    }

    /// <summary>Unwraps a reactive reference or returns a non-reference unchanged.</summary>
    /// <param name="value">The value to inspect.</param>
    /// <returns>The current reference value or the original value.</returns>
    public static object? _unref(object? value)
    {
        return value is IReactiveReference reference ? reference.Value : value;
    }

    /// <summary>Determines whether a value is a reactive reference.</summary>
    /// <param name="value">The value to inspect.</param>
    /// <returns>True when the value is a reactive reference.</returns>
    public static bool _isRef(object? value)
    {
        return Reactive.IsRef(value);
    }

    /// <summary>Creates the generated property-bag representation.</summary>
    /// <param name="entries">The ordered name/value entries.</param>
    /// <returns>An immutable property snapshot.</returns>
    public static IReadOnlyDictionary<string, object?> _createProps(
        params (string Name, object? Value)[] entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Dictionary<string, object?> properties =
            new(entries.Length, StringComparer.Ordinal);
        foreach ((string name, object? value) in entries)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            properties[name] = value;
        }

        return new ReadOnlyDictionary<string, object?>(properties);
    }

    /// <summary>Target-types a value-returning event handler with a payload.</summary>
    /// <param name="handler">The handler.</param>
    /// <returns>The unchanged handler.</returns>
    public static Func<object?, object?> _withHandler(Func<object?, object?> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return handler;
    }

    /// <summary>Target-types a synchronous event handler with a payload.</summary>
    /// <param name="handler">The handler.</param>
    /// <returns>The unchanged handler.</returns>
    public static Action<object?> _withHandler(Action<object?> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return handler;
    }

    /// <summary>Target-types a parameterless synchronous event handler.</summary>
    /// <param name="handler">The handler.</param>
    /// <returns>The unchanged handler.</returns>
    public static Action _withHandler(Action handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return handler;
    }

    /// <summary>Target-types a parameterless value-returning event handler.</summary>
    /// <param name="handler">The handler.</param>
    /// <returns>The unchanged handler.</returns>
    public static Func<object?> _withHandler(Func<object?> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return handler;
    }

    /// <summary>Target-types a parameterless task-returning event handler.</summary>
    /// <param name="handler">The handler.</param>
    /// <returns>The unchanged task-returning handler.</returns>
    public static Func<Task> _withHandler(Func<Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return handler;
    }

    /// <summary>Target-types a task-returning event handler with an object payload.</summary>
    /// <param name="handler">The handler.</param>
    /// <returns>The unchanged task-returning handler.</returns>
    public static Func<object?, Task> _withHandler(Func<object?, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return handler;
    }

    /// <summary>
    /// Adapts a task-returning handler with a strongly typed payload to the component event contract.
    /// </summary>
    /// <typeparam name="TEvent">The payload type.</typeparam>
    /// <param name="handler">The strongly typed handler.</param>
    /// <returns>An object-payload task-returning handler.</returns>
    public static Func<object?, Task> _withHandler<TEvent>(Func<TEvent, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return value => handler((TEvent)value!);
    }

    /// <summary>
    /// Adapts a synchronous handler with a strongly typed payload to the component event contract.
    /// </summary>
    /// <typeparam name="TEvent">The payload type.</typeparam>
    /// <param name="handler">The strongly typed handler.</param>
    /// <returns>An object-payload synchronous handler.</returns>
    public static Action<object?> _withHandler<TEvent>(Action<TEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return value => handler((TEvent)value!);
    }

    /// <summary>Target-types a delegate shape not covered by a more specific overload.</summary>
    /// <param name="handler">The handler.</param>
    /// <returns>The unchanged handler.</returns>
    public static Delegate _withHandler(Delegate handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return handler;
    }

    /// <summary>Resumes block tracking after writing a generated cache slot.</summary>
    /// <param name="index">The cache slot index.</param>
    /// <param name="tracking">The suspension token.</param>
    /// <param name="value">The cached value.</param>
    /// <returns><paramref name="value"/>.</returns>
    public static object? _setCache(int index, BlockToken tracking, object? value)
    {
        _ = index;
        _blockTrackingDepth -= tracking.TrackingDelta;
        return value;
    }

    /// <summary>Clones a cached array before it is reused as generated children.</summary>
    /// <param name="value">The cached value.</param>
    /// <returns>A shallow array clone, or the original non-array value.</returns>
    public static object? _spreadCache(object? value)
    {
        return value is Array array ? array.Clone() : value;
    }

    /// <summary>Memoizes a generated subtree against a dependency array.</summary>
    /// <param name="dependencies">The current memo dependencies.</param>
    /// <param name="render">The subtree factory.</param>
    /// <param name="cache">The component instance's render cache.</param>
    /// <param name="index">The cache slot index.</param>
    /// <returns>The cached or newly rendered subtree.</returns>
    public static IComponent _withMemo(
        object?[] dependencies,
        Func<object?> render,
        object?[] cache,
        int index)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (index >= cache.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (cache[index] is IComponent cached && _isMemoSame(cached, dependencies))
        {
            return cached;
        }

        IComponent component = NormalizeRoot(render());
        cache[index] = component;
        _memoMetadata.Remove(component);
        _memoMetadata.Add(component, new MemoMetadata(dependencies));
        return component;
    }

    /// <summary>Compares a cached tree value's memo dependencies with the current dependencies.</summary>
    /// <param name="cached">The cached value.</param>
    /// <param name="dependencies">The current dependencies.</param>
    /// <returns>True when every dependency is unchanged.</returns>
    public static bool _isMemoSame(object? cached, object?[] dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        if (cached is not IComponent component
            || !_memoMetadata.TryGetValue(component, out MemoMetadata? metadata)
            || metadata.Dependencies.Length != dependencies.Length)
        {
            return false;
        }

        for (int index = 0; index < dependencies.Length; index++)
        {
            if (!Equals(metadata.Dependencies[index], dependencies[index]))
            {
                return false;
            }
        }

        TrackBlockRoot(component);
        return true;
    }

    /// <summary>Normalizes a generated render result into one component-tree root.</summary>
    /// <param name="renderResult">The generated render result.</param>
    /// <returns>The normalized tree root.</returns>
    public static IComponent NormalizeRoot(object? renderResult)
    {
        return NormalizeChild(renderResult);
    }

    internal static void ClearBlockTrackingAfterRenderFailure()
    {
        BlockFrames.Clear();
        _blockTrackingDepth = 1;
    }

    internal static void ResetBlockTrackingForTests()
    {
        ClearBlockTrackingAfterRenderFailure();
        _memoMetadata = new ConditionalWeakTable<IComponent, MemoMetadata>();
    }

    private static IComponent CreateComponent(
        object? tag,
        object? properties,
        object? children,
        PatchFlags patchFlags,
        string[]? dynamicProperties,
        bool asBlock)
    {
        return tag switch
        {
            string elementTag => CreateElement(
                elementTag,
                properties,
                children,
                patchFlags,
                dynamicProperties,
                asBlock),
            NamedTemplateType namedTemplate => CreateTemplate(
                namedTemplate.Name,
                properties,
                children,
                patchFlags,
                dynamicProperties,
                asBlock),
            Type templateType => CreateTemplate(
                templateType,
                properties,
                children,
                patchFlags,
                dynamicProperties,
                asBlock),
            DynamicComponentName dynamicName => CreateTemplate(
                dynamicName.Name,
                properties,
                children,
                patchFlags,
                dynamicProperties,
                asBlock),
            AsynchronousComponentDefinition asynchronousDefinition => CreateTemplate(
                asynchronousDefinition.ComponentType,
                properties,
                children,
                patchFlags,
                dynamicProperties,
                asBlock),
            BuiltInComponentType { Name: "Fragment" } => CreateFragment(
                properties,
                children,
                patchFlags,
                dynamicProperties,
                asBlock),
            BuiltInComponentType { Name: "Teleport" } => CreateTeleport(
                properties,
                children,
                patchFlags,
                dynamicProperties,
                asBlock),
            BuiltInComponentType builtIn => throw new NotSupportedException(
                $"The built-in component <{builtIn.Name}> is not implemented."),
            null => CreateComment(asBlock),
            _ => throw new NotSupportedException(
                $"Unsupported component tag of type '{tag.GetType().Name}'."),
        };
    }

    private static IComponent CreateElement(
        string tag,
        object? properties,
        object? children,
        PatchFlags patchFlags,
        string[]? dynamicProperties,
        bool asBlock)
    {
        object? key = GetProperty(properties, "key");
        IComponentReference? reference = ResolveTemplateReference(properties);
        ComponentAttributes attributes = BuildAttributes(properties);
        IReadOnlyList<IComponent> childComponents = CoerceChildren(children);
        if (asBlock)
        {
            return CompleteBlock(
                optimization => new ElementComponent(
                    tag,
                    attributes,
                    childComponents,
                    key,
                    optimization,
                    directives: null,
                    reference: reference),
                patchFlags,
                dynamicProperties);
        }

        IComponent component = new ElementComponent(
            tag,
            attributes,
            childComponents,
            key,
            new ComponentOptimization(patchFlags, dynamicProperties),
            directives: null,
            reference: reference);
        TrackDynamicComponent(component);
        return component;
    }

    private static IComponent CreateTemplate(
        object template,
        object? properties,
        object? children,
        PatchFlags patchFlags,
        string[]? dynamicProperties,
        bool asBlock)
    {
        object? key = GetProperty(properties, "key");
        IComponentReference? reference = ResolveTemplateReference(properties);
        ComponentArguments arguments = BuildTemplateArguments(properties);
        IReadOnlyDictionary<string, ComponentEventListener>? listeners =
            BuildComponentListeners(properties);
        IReadOnlyDictionary<string, ComponentSlot>? slots = BuildSlots(children);

        IComponent Factory(ComponentOptimization optimization)
        {
            return template switch
            {
                Type type => new TemplateComponent(
                    type,
                    arguments,
                    slots,
                    key,
                    optimization,
                    listeners,
                    directives: null,
                    reference: reference),
                string name => new TemplateComponent(
                    name,
                    arguments,
                    slots,
                    key,
                    optimization,
                    listeners,
                    directives: null,
                    reference: reference),
                _ => throw new InvalidOperationException("Unsupported template selector."),
            };
        }

        if (asBlock)
        {
            return CompleteBlock(Factory, patchFlags, dynamicProperties);
        }

        IComponent component = Factory(
            new ComponentOptimization(patchFlags, dynamicProperties));
        TrackDynamicComponent(component);
        return component;
    }

    private static IComponent CreateFragment(
        object? properties,
        object? children,
        PatchFlags patchFlags,
        string[]? dynamicProperties,
        bool asBlock)
    {
        object? key = GetProperty(properties, "key");
        IReadOnlyList<IComponent> childComponents = CoerceChildren(children);
        if (asBlock)
        {
            return CompleteBlock(
                optimization => new FragmentComponent(
                    childComponents,
                    key,
                    optimization),
                patchFlags,
                dynamicProperties);
        }

        IComponent component = new FragmentComponent(
            childComponents,
            key,
            new ComponentOptimization(patchFlags, dynamicProperties));
        TrackDynamicComponent(component);
        return component;
    }

    private static IComponent CreateTeleport(
        object? properties,
        object? children,
        PatchFlags patchFlags,
        string[]? dynamicProperties,
        bool asBlock)
    {
        object? target = GetProperty(properties, "to")
            ?? throw new InvalidOperationException("A Teleport requires a non-null 'to' property.");
        object? key = GetProperty(properties, "key");
        bool isDisabled = StyleAndClassNormalization.IsTruthy(
            GetProperty(properties, "disabled"));
        bool isDeferred = StyleAndClassNormalization.IsTruthy(
            GetProperty(properties, "defer"));
        IReadOnlyList<IComponent> childComponents = CoerceChildren(children);
        if (asBlock)
        {
            return CompleteBlock(
                optimization => new TeleportComponent(
                    target,
                    childComponents,
                    isDisabled,
                    key,
                    optimization,
                    isDeferred),
                patchFlags,
                dynamicProperties);
        }

        IComponent component = new TeleportComponent(
            target,
            childComponents,
            isDisabled,
            key,
            new ComponentOptimization(patchFlags, dynamicProperties),
            isDeferred);
        TrackDynamicComponent(component);
        return component;
    }

    private static IComponent CreateComment(bool asBlock)
    {
        if (!asBlock)
        {
            return ComponentTree.Comment();
        }

        return CompleteBlock(
            optimization => new RuntimeCommentComponent(null, optimization),
            default,
            null);
    }

    private static IComponentReference? ResolveTemplateReference(
        object? properties)
    {
        Action<string>? warningHandler =
            ComponentContext.Current?.Application.WarnHandler;
        return TemplateReference.FromValue(
            GetProperty(properties, "ref"),
            warningHandler);
    }

    private static TComponent CompleteBlock<TComponent>(
        Func<ComponentOptimization, TComponent> factory,
        PatchFlags patchFlags,
        string[]? dynamicProperties)
        where TComponent : IComponent
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (BlockFrames.Count == 0)
        {
            throw new InvalidOperationException(
                "A block factory was invoked without a matching _openBlock call.");
        }

        BlockFrame frame = BlockFrames[^1];
        BlockFrames.RemoveAt(BlockFrames.Count - 1);
        IReadOnlyList<IComponent>? dynamicChildren = _blockTrackingDepth > 0
            ? frame.DynamicChildren is not null
                ? frame.DynamicChildren
                : Array.Empty<IComponent>()
            : null;
        ComponentOptimization optimization = new(
            patchFlags,
            dynamicProperties,
            dynamicChildren,
            frame.HasOnce);
        TComponent component = factory(optimization);
        TrackBlockRoot(component);
        return component;
    }

    private static void TrackDynamicComponent(IComponent component)
    {
        PatchFlags patchFlags = component.Optimization.PatchFlags;
        bool shouldTrack =
            component.Kind == ComponentKind.Template
            || (patchFlags > 0 && patchFlags != PatchFlags.NeedHydration);
        if (shouldTrack)
        {
            TrackBlockRoot(component);
        }
    }

    private static void TrackBlockRoot(IComponent component)
    {
        if (_blockTrackingDepth <= 0 || BlockFrames.Count == 0)
        {
            return;
        }

        BlockFrames[^1].DynamicChildren?.Add(component);
    }

    private static void ReplaceTrackedComponent(IComponent existing, IComponent replacement)
    {
        if (BlockFrames.Count == 0)
        {
            return;
        }

        List<IComponent>? dynamicChildren = BlockFrames[^1].DynamicChildren;
        if (dynamicChildren is null)
        {
            return;
        }

        for (int index = dynamicChildren.Count - 1; index >= 0; index--)
        {
            if (ReferenceEquals(dynamicChildren[index], existing))
            {
                dynamicChildren[index] = replacement;
                return;
            }
        }
    }

    private static IReadOnlyList<IComponent> CoerceChildren(object? children)
    {
        if (children is null)
        {
            return Array.Empty<IComponent>();
        }

        if (children is string text)
        {
            return new IComponent[] { ComponentTree.Text(text) };
        }

        if (children is IComponent component)
        {
            return new IComponent[] { component };
        }

        if (children is IEnumerable enumerable && children is not IDictionary)
        {
            List<IComponent> result = new();
            foreach (object? child in enumerable)
            {
                result.Add(NormalizeChild(child));
            }

            return result.Count == 0
                ? Array.Empty<IComponent>()
                : new ReadOnlyCollection<IComponent>(result);
        }

        return new IComponent[] { NormalizeChild(children) };
    }

    private static IComponent NormalizeChild(object? child)
    {
        return child switch
        {
            null => ComponentTree.Comment(),
            bool => ComponentTree.Comment(),
            IComponent component => component,
            string text => ComponentTree.Text(text),
            IEnumerable enumerable when child is not IDictionary => ComponentTree.Fragment(
                CoerceEnumerableChildren(enumerable)),
            _ => ComponentTree.Text(DisplayStringFormatter.ToDisplayString(child)),
        };
    }

    private static IReadOnlyList<IComponent> CoerceEnumerableChildren(IEnumerable values)
    {
        List<IComponent> result = new();
        foreach (object? value in values)
        {
            result.Add(NormalizeChild(value));
        }

        return result.Count == 0
            ? Array.Empty<IComponent>()
            : new ReadOnlyCollection<IComponent>(result);
    }

    private static IComponent? NormalizeSlotResult(object?[]? values)
    {
        if (values is null || values.Length == 0)
        {
            return null;
        }

        IReadOnlyList<IComponent> children = CoerceChildren(values);
        return children.Count == 1
            ? children[0]
            : ComponentTree.Fragment(children);
    }

    private static ComponentAttributes BuildAttributes(object? properties)
    {
        List<IComponentAttribute> attributes = new();
        foreach (KeyValuePair<string, object?> property in ReadProperties(properties))
        {
            if (!string.Equals(property.Key, "key", StringComparison.Ordinal)
                && !string.Equals(property.Key, "ref", StringComparison.Ordinal))
            {
                attributes.Add(new ComponentAttribute(property.Key, property.Value));
            }
        }

        return new ComponentAttributes(attributes);
    }

    private static ComponentArguments BuildTemplateArguments(object? properties)
    {
        List<KeyValuePair<string, object?>> arguments = new();
        foreach (KeyValuePair<string, object?> property in ReadProperties(properties))
        {
            if (!string.Equals(property.Key, "key", StringComparison.Ordinal)
                && !string.Equals(property.Key, "ref", StringComparison.Ordinal))
            {
                arguments.Add(property);
            }
        }

        return new ComponentArguments(arguments);
    }

    private static IReadOnlyDictionary<string, ComponentEventListener>? BuildComponentListeners(
        object? properties)
    {
        Dictionary<string, ComponentEventListener>? listeners = null;
        foreach (KeyValuePair<string, object?> property in ReadProperties(properties))
        {
            if (!IsEventListenerName(property.Key)
                || IsComponentNodeLifecycleName(property.Key)
                || property.Value is null)
            {
                continue;
            }

            listeners ??= new Dictionary<string, ComponentEventListener>(StringComparer.Ordinal);
            listeners[ToEventName(property.Key)] = CreateComponentEventListener(property.Value);
        }

        return listeners;
    }

    private static ComponentEventListener CreateComponentEventListener(object handler)
    {
        return handler switch
        {
            ComponentEventListener listener => listener,
            AsynchronousComponentEventArgumentsHandler asynchronousArguments =>
                ComponentEventListener.ForAsynchronousArguments(asynchronousArguments),
            ComponentEventArgumentsHandler synchronousArguments =>
                ComponentEventListener.ForArguments(synchronousArguments),
            AsynchronousComponentEventHandler asynchronous =>
                new ComponentEventListener(asynchronous),
            ComponentEventHandler synchronous =>
                new ComponentEventListener(synchronous),
            Func<object?, Task> asynchronous =>
                new ComponentEventListener(value => InvokeAllAsync(asynchronous, value)),
            Func<Task> asynchronous =>
                new ComponentEventListener(_ => InvokeAllAsync(asynchronous)),
            Action<object?> synchronous =>
                new ComponentEventListener(value => synchronous(value)),
            Action synchronous =>
                new ComponentEventListener(_ => synchronous()),
            Func<object?, object?> valueHandler =>
                new ComponentEventListener(value => CoerceHandlerTask(valueHandler(value))),
            Func<object?> valueHandler =>
                new ComponentEventListener(_ => CoerceHandlerTask(valueHandler())),
            _ => throw new NotSupportedException(
                $"Component event handler type '{handler.GetType().Name}' is not supported. " +
                "Wrap strongly typed handlers with _withHandler."),
        };
    }

    private static Task InvokeAllAsync(Func<object?, Task> handler, object? value)
    {
        Delegate[] invocationList = handler.GetInvocationList();
        if (invocationList.Length == 1)
        {
            return handler(value);
        }

        Task[] tasks = new Task[invocationList.Length];
        for (int index = 0; index < invocationList.Length; index++)
        {
            tasks[index] = ((Func<object?, Task>)invocationList[index])(value);
        }

        return Task.WhenAll(tasks);
    }

    private static Task InvokeAllAsync(Func<Task> handler)
    {
        Delegate[] invocationList = handler.GetInvocationList();
        if (invocationList.Length == 1)
        {
            return handler();
        }

        Task[] tasks = new Task[invocationList.Length];
        for (int index = 0; index < invocationList.Length; index++)
        {
            tasks[index] = ((Func<Task>)invocationList[index])();
        }

        return Task.WhenAll(tasks);
    }

    private static Task CoerceHandlerTask(object? value)
    {
        return value as Task ?? Task.CompletedTask;
    }

    private static IReadOnlyDictionary<string, ComponentSlot>? BuildSlots(object? children)
    {
        if (children is null)
        {
            return null;
        }

        if (children is IReadOnlyDictionary<string, ComponentSlot> typedSlots)
        {
            return typedSlots;
        }

        if (CanReadProperties(children))
        {
            ComponentSlots slots = new(ReadSlotFlags(children));
            foreach (KeyValuePair<string, object?> property in ReadProperties(children))
            {
                if (string.Equals(property.Key, "_", StringComparison.Ordinal))
                {
                    continue;
                }

                if (TryCoerceSlot(property.Value, out ComponentSlot? slot))
                {
                    slots[property.Key] = slot!;
                }
            }

            return slots.Count == 0 ? null : slots;
        }

        IComponent? content = NormalizeSlotContent(children);
        if (content is null)
        {
            return null;
        }

        return new ComponentSlots
        {
            ["default"] = _ => content,
        };
    }

    private static SlotFlags ReadSlotFlags(object slots)
    {
        object? value = GetProperty(slots, "_");
        if (value is SlotFlags flags)
        {
            return flags;
        }

        if (value is int number && Enum.IsDefined((SlotFlags)number))
        {
            return (SlotFlags)number;
        }

        return SlotFlags.Stable;
    }

    private static bool HasRenderableSlotContent(IComponent? component)
    {
        if (component is null || component.Kind == ComponentKind.Comment)
        {
            return false;
        }

        if (component is not IFragmentComponent fragment)
        {
            return true;
        }

        for (int index = 0; index < fragment.Children.Count; index++)
        {
            if (HasRenderableSlotContent(fragment.Children[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryCoerceSlot(object? value, out ComponentSlot? slot)
    {
        switch (value)
        {
            case ComponentSlot componentSlot:
                slot = componentSlot;
                return true;
            case Func<object?[]?> plain:
                slot = _withCtx(plain);
                return true;
            case Func<object?, object?[]?> scoped:
                slot = _withCtx(scoped);
                return true;
            default:
                slot = null;
                return false;
        }
    }

    private static IComponent? NormalizeSlotContent(object? content)
    {
        if (content is null)
        {
            return null;
        }

        IReadOnlyList<IComponent> children = CoerceChildren(content);
        return children.Count switch
        {
            0 => null,
            1 => children[0],
            _ => ComponentTree.Fragment(children),
        };
    }

    private static void MergeDynamicSlot(
        IDictionary<string, object?> destination,
        object? value)
    {
        if (value is null)
        {
            return;
        }

        if (CanReadProperties(value))
        {
            Dictionary<string, object?> descriptor = CopyProperties(value);
            if (descriptor.TryGetValue("name", out object? nameValue)
                && nameValue is string name
                && descriptor.TryGetValue("fn", out object? function)
                && TryCoerceSlot(function, out ComponentSlot? slot))
            {
                destination[name] = slot;
            }

            return;
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            foreach (object? item in enumerable)
            {
                MergeDynamicSlot(destination, item);
            }
        }
    }

    private static ComponentArguments BuildArguments(object? properties)
    {
        return new ComponentArguments(ReadProperties(properties));
    }

    private static ComponentDirectiveBinding CreateDirectiveBinding(object?[] tuple)
    {
        string name;
        object? source = tuple[0];
        IComponentDirectiveBinding? existing = source as IComponentDirectiveBinding;
        switch (source)
        {
            case DirectiveReference reference:
                name = reference.Name;
                break;
            case string text:
                name = text;
                break;
            case IComponentDirectiveBinding binding:
                name = binding.DirectiveName;
                break;
            default:
                throw new NotSupportedException(
                    $"Directive reference type '{source?.GetType().Name}' is not supported.");
        }

        object? value = tuple.Length > 1 ? tuple[1] : existing?.Value;
        string? argument = tuple.Length > 2 ? tuple[2] as string : existing?.Argument;
        IReadOnlyDictionary<string, bool>? modifiers =
            tuple.Length > 3
                ? tuple[3] as IReadOnlyDictionary<string, bool>
                : existing?.Modifiers;
        return new ComponentDirectiveBinding(name, value, argument, modifiers);
    }

    private static Dictionary<string, object?> CopyProperties(object? source)
    {
        Dictionary<string, object?> result = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> property in ReadProperties(source))
        {
            result[property.Key] = property.Value;
        }

        return result;
    }

    private static IEnumerable<KeyValuePair<string, object?>> ReadProperties(object? source)
    {
        switch (source)
        {
            case null:
                yield break;
            case IReadOnlyDictionary<string, object?> dictionary:
                foreach (KeyValuePair<string, object?> property in dictionary)
                {
                    yield return property;
                }

                yield break;
            case IEnumerable<KeyValuePair<string, object?>> values:
                foreach (KeyValuePair<string, object?> property in values)
                {
                    yield return property;
                }

                yield break;
            case IComponentAttributeCollection attributes:
                foreach (IComponentAttribute attribute in attributes)
                {
                    yield return new KeyValuePair<string, object?>(
                        attribute.Name,
                        attribute.Value);
                }

                yield break;
            default:
                yield break;
        }
    }

    private static bool CanReadProperties(object? value)
    {
        return value is IReadOnlyDictionary<string, object?>
            || value is IEnumerable<KeyValuePair<string, object?>>
            || value is IComponentAttributeCollection;
    }

    private static object? GetProperty(object? properties, string name)
    {
        foreach (KeyValuePair<string, object?> property in ReadProperties(properties))
        {
            if (string.Equals(property.Key, name, StringComparison.Ordinal))
            {
                return property.Value;
            }
        }

        return null;
    }

    private static bool IsEventListenerName(string name)
    {
        return name.Length > 2
            && name[0] == 'o'
            && name[1] == 'n'
            && char.IsAsciiLetterUpper(name[2]);
    }

    private static bool IsComponentNodeLifecycleName(string name)
    {
        return name.StartsWith("onVnode", StringComparison.Ordinal);
    }

    private static string ToEventName(string listenerName)
    {
        string name = listenerName[2..];
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static string CoerceText(object? text)
    {
        return text switch
        {
            null => string.Empty,
            string value => value,
            _ => DisplayStringFormatter.ToDisplayString(text),
        };
    }

    private sealed class BlockFrame
    {
        internal BlockFrame(bool disableTracking)
        {
            DynamicChildren = disableTracking ? null : new List<IComponent>();
        }

        internal List<IComponent>? DynamicChildren { get; }

        internal bool HasOnce { get; set; }
    }

    private sealed class BuiltInComponentType
    {
        internal BuiltInComponentType(string name)
        {
            Name = name;
        }

        internal string Name { get; }
    }

    private sealed class NamedTemplateType
    {
        internal NamedTemplateType(string name)
        {
            Name = name;
        }

        internal string Name { get; }
    }

    private sealed class DirectiveReference
    {
        internal DirectiveReference(string name)
        {
            Name = name;
        }

        internal string Name { get; }
    }

    private sealed class MemoMetadata
    {
        internal MemoMetadata(object?[] dependencies)
        {
            Dependencies = (object?[])dependencies.Clone();
        }

        internal object?[] Dependencies { get; }
    }

    private sealed class RuntimeCommentComponent : ICommentComponent
    {
        internal RuntimeCommentComponent(
            string? text,
            ComponentOptimization optimization)
        {
            Text = text;
            Optimization = optimization;
        }

        public ComponentKind Kind => ComponentKind.Comment;

        public object? Key => null;

        public ComponentOptimization Optimization { get; }

        public string? Text { get; }
    }
}
