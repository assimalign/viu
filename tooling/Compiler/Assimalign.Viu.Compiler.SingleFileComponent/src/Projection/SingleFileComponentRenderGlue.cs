namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>
/// The tier-three render glue a compiled component calls: the one-per-consumer-assembly source that
/// declares <c>Assimalign.Viu.Generated.RenderGlue</c>. Specified by <c>[SFC-CG-2]</c> through
/// <c>[SFC-CG-4]</c>; the glue calls only the adopted public runtime surface and BCL contracts, so no
/// state or type identity is copied across assemblies.
/// </summary>
/// <remarks>
/// The text lives here, beside the projection both hosts share, because both must emit the SAME glue.
/// The source generator writes it into the consumer assembly at build time, and the language service
/// adds it to the editor compilation, which has no generator run to produce it. Were the editor
/// without it, every emitted <c>RenderGlue</c> call would bind to an error type and take the surrounding
/// expression down with it: a <c>v-for</c> source is unwrapped through this glue, so its alias — and
/// every member of that alias — would have no type the editor could name.
/// </remarks>
public static class SingleFileComponentRenderGlue
{
    /// <summary>The namespace the glue declares, as the emitted call sites qualify it.</summary>
    public const string GeneratedNamespace = "Assimalign.Viu.Generated";

    /// <summary>
    /// The complete glue source, leading blank line included, exactly as a consumer assembly
    /// receives it — appended after the registration catalog by the build, added as its own
    /// compilation unit by the editor.
    /// </summary>
    public const string Source = """

        namespace Assimalign.Viu.Generated
        {
            [global::System.CodeDom.Compiler.GeneratedCode("Assimalign.Viu.Generators.Syntax", "1.0.0.0")]
            internal static class RenderGlue
            {
                internal static global::System.Action Handler(global::System.Action handler)
                {
                    global::System.ArgumentNullException.ThrowIfNull(handler);
                    return handler;
                }

                internal static global::System.Action<object?> Handler(global::System.Action<object?> handler)
                {
                    global::System.ArgumentNullException.ThrowIfNull(handler);
                    return handler;
                }

                internal static global::System.Func<object?> Handler(global::System.Func<object?> handler)
                {
                    global::System.ArgumentNullException.ThrowIfNull(handler);
                    return handler;
                }

                internal static global::System.Func<object?, object?> Handler(global::System.Func<object?, object?> handler)
                {
                    global::System.ArgumentNullException.ThrowIfNull(handler);
                    return handler;
                }

                internal static global::System.Func<global::System.Threading.Tasks.Task> Handler(
                    global::System.Func<global::System.Threading.Tasks.Task> handler)
                {
                    global::System.ArgumentNullException.ThrowIfNull(handler);
                    return handler;
                }

                internal static global::System.Func<object?, global::System.Threading.Tasks.Task> Handler(
                    global::System.Func<object?, global::System.Threading.Tasks.Task> handler)
                {
                    global::System.ArgumentNullException.ThrowIfNull(handler);
                    return handler;
                }

                internal static global::System.Func<object?, global::System.Threading.Tasks.Task> Handler<TEvent>(
                    global::System.Func<TEvent, global::System.Threading.Tasks.Task> handler)
                {
                    global::System.ArgumentNullException.ThrowIfNull(handler);
                    return value => handler((TEvent)value!);
                }

                internal static global::System.Action<object?> Handler<TEvent>(global::System.Action<TEvent> handler)
                {
                    global::System.ArgumentNullException.ThrowIfNull(handler);
                    return value => handler((TEvent)value!);
                }

                internal static global::System.Delegate Handler(global::System.Delegate handler)
                {
                    global::System.ArgumentNullException.ThrowIfNull(handler);
                    return handler;
                }

                internal static TValue Unwrap<TValue>(
                    global::Assimalign.Viu.Reactivity.IReactiveReference<TValue> reference)
                {
                    global::System.ArgumentNullException.ThrowIfNull(reference);
                    return reference.Value;
                }

                internal static object? Unwrap(object? value)
                    => value is global::Assimalign.Viu.Reactivity.IReactiveReference reference
                        ? reference.Value
                        : value;

                internal static global::System.Type ParameterRuntimeType<TValue>()
                    => typeof(TValue);

                internal static bool TryReadParameter<TValue>(
                    global::System.Collections.Generic.IReadOnlyDictionary<string, object?> parameters,
                    string name,
                    out TValue value)
                {
                    if (!parameters.TryGetValue(name, out object? supplied))
                    {
                        value = default!;
                        return false;
                    }

                    value = supplied is TValue typed ? typed : default!;
                    return true;
                }

                internal static global::Assimalign.Viu.Components.ComponentEventListener ComponentListener(
                    global::System.Action handler)
                {
                    global::System.ArgumentNullException.ThrowIfNull(handler);
                    return arguments => handler();
                }

                internal static global::Assimalign.Viu.Components.ComponentEventListener ComponentListener(
                    global::System.Action<object?> handler)
                {
                    global::System.ArgumentNullException.ThrowIfNull(handler);
                    return arguments => handler(FirstArgument(arguments));
                }

                internal static global::Assimalign.Viu.Components.ComponentEventListener ComponentListener(
                    global::System.Func<object?> handler)
                {
                    global::System.ArgumentNullException.ThrowIfNull(handler);
                    return arguments => handler();
                }

                internal static global::Assimalign.Viu.Components.ComponentEventListener ComponentListener(
                    global::System.Func<object?, object?> handler)
                {
                    global::System.ArgumentNullException.ThrowIfNull(handler);
                    return arguments => handler(FirstArgument(arguments));
                }

                internal static global::Assimalign.Viu.Components.ComponentEventListener ComponentListener<TEvent>(
                    global::System.Action<TEvent> handler)
                {
                    global::System.ArgumentNullException.ThrowIfNull(handler);
                    return arguments => handler((TEvent)FirstArgument(arguments)!);
                }

                internal static void OnBeforeMount(
                    global::Assimalign.Viu.Components.ComponentContext context,
                    global::System.Action callback) => context.Lifecycle.OnBeforeMount(callback);

                internal static void OnBeforeMount(
                    global::Assimalign.Viu.Components.ComponentContext context,
                    global::System.Func<global::System.Threading.Tasks.Task> callback) =>
                    context.Lifecycle.OnBeforeMount(callback);

                internal static void OnBeforeMount(
                    global::Assimalign.Viu.Components.ComponentContext context,
                    global::System.Func<global::System.Threading.CancellationToken, global::System.Threading.Tasks.Task> callback) =>
                    context.Lifecycle.OnBeforeMount(callback);

                internal static void OnMounted(
                    global::Assimalign.Viu.Components.ComponentContext context,
                    global::System.Action callback) => context.Lifecycle.OnMounted(callback);

                internal static void OnMounted(
                    global::Assimalign.Viu.Components.ComponentContext context,
                    global::System.Func<global::System.Threading.Tasks.Task> callback) => context.Lifecycle.OnMounted(callback);

                internal static void OnMounted(
                    global::Assimalign.Viu.Components.ComponentContext context,
                    global::System.Func<global::System.Threading.CancellationToken, global::System.Threading.Tasks.Task> callback) =>
                    context.Lifecycle.OnMounted(callback);

                internal static void OnBeforeUpdate(
                    global::Assimalign.Viu.Components.ComponentContext context,
                    global::System.Action callback) => context.Lifecycle.OnBeforeUpdate(callback);

                internal static void OnBeforeUpdate(
                    global::Assimalign.Viu.Components.ComponentContext context,
                    global::System.Func<global::System.Threading.Tasks.Task> callback) =>
                    context.Lifecycle.OnBeforeUpdate(callback);

                internal static void OnBeforeUpdate(
                    global::Assimalign.Viu.Components.ComponentContext context,
                    global::System.Func<global::System.Threading.CancellationToken, global::System.Threading.Tasks.Task> callback) =>
                    context.Lifecycle.OnBeforeUpdate(callback);

                internal static void OnUpdated(
                    global::Assimalign.Viu.Components.ComponentContext context,
                    global::System.Action callback) => context.Lifecycle.OnUpdated(callback);

                internal static void OnUpdated(
                    global::Assimalign.Viu.Components.ComponentContext context,
                    global::System.Func<global::System.Threading.Tasks.Task> callback) => context.Lifecycle.OnUpdated(callback);

                internal static void OnUpdated(
                    global::Assimalign.Viu.Components.ComponentContext context,
                    global::System.Func<global::System.Threading.CancellationToken, global::System.Threading.Tasks.Task> callback) =>
                    context.Lifecycle.OnUpdated(callback);

                internal static void OnBeforeUnmount(
                    global::Assimalign.Viu.Components.ComponentContext context,
                    global::System.Action callback) => context.Lifecycle.OnBeforeUnmount(callback);

                internal static void OnBeforeUnmount(
                    global::Assimalign.Viu.Components.ComponentContext context,
                    global::System.Func<global::System.Threading.Tasks.Task> callback) =>
                    context.Lifecycle.OnBeforeUnmount(callback);

                internal static void OnBeforeUnmount(
                    global::Assimalign.Viu.Components.ComponentContext context,
                    global::System.Func<global::System.Threading.CancellationToken, global::System.Threading.Tasks.Task> callback) =>
                    context.Lifecycle.OnBeforeUnmount(callback);

                internal static void OnUnmounted(
                    global::Assimalign.Viu.Components.ComponentContext context,
                    global::System.Action callback) => context.Lifecycle.OnUnmounted(callback);

                internal static void OnUnmounted(
                    global::Assimalign.Viu.Components.ComponentContext context,
                    global::System.Func<global::System.Threading.Tasks.Task> callback) => context.Lifecycle.OnUnmounted(callback);

                internal static void OnUnmounted(
                    global::Assimalign.Viu.Components.ComponentContext context,
                    global::System.Func<global::System.Threading.CancellationToken, global::System.Threading.Tasks.Task> callback) =>
                    context.Lifecycle.OnUnmounted(callback);

                internal static void OnActivated(
                    global::Assimalign.Viu.Components.ComponentContext context,
                    global::System.Action callback) => context.Lifecycle.OnActivated(callback);

                internal static void OnActivated(
                    global::Assimalign.Viu.Components.ComponentContext context,
                    global::System.Func<global::System.Threading.Tasks.Task> callback) => context.Lifecycle.OnActivated(callback);

                internal static void OnActivated(
                    global::Assimalign.Viu.Components.ComponentContext context,
                    global::System.Func<global::System.Threading.CancellationToken, global::System.Threading.Tasks.Task> callback) =>
                    context.Lifecycle.OnActivated(callback);

                internal static void OnDeactivated(
                    global::Assimalign.Viu.Components.ComponentContext context,
                    global::System.Action callback) => context.Lifecycle.OnDeactivated(callback);

                internal static void OnDeactivated(
                    global::Assimalign.Viu.Components.ComponentContext context,
                    global::System.Func<global::System.Threading.Tasks.Task> callback) => context.Lifecycle.OnDeactivated(callback);

                internal static void OnDeactivated(
                    global::Assimalign.Viu.Components.ComponentContext context,
                    global::System.Func<global::System.Threading.CancellationToken, global::System.Threading.Tasks.Task> callback) =>
                    context.Lifecycle.OnDeactivated(callback);

                internal static void OnServerPrefetch(
                    global::Assimalign.Viu.Components.ComponentContext context,
                    global::System.Action callback) => context.Lifecycle.OnServerPrefetch(callback);

                internal static void OnServerPrefetch(
                    global::Assimalign.Viu.Components.ComponentContext context,
                    global::System.Func<global::System.Threading.Tasks.Task> callback) =>
                    context.Lifecycle.OnServerPrefetch(callback);

                internal static void OnServerPrefetch(
                    global::Assimalign.Viu.Components.ComponentContext context,
                    global::System.Func<global::System.Threading.CancellationToken, global::System.Threading.Tasks.Task> callback) =>
                    context.Lifecycle.OnServerPrefetch(callback);

                internal static void OnErrorCaptured(
                    global::Assimalign.Viu.Components.ComponentContext context,
                    global::System.Func<global::System.Exception, global::Assimalign.Viu.Components.ComponentContext?, string, bool> callback) =>
                    context.Lifecycle.OnErrorCaptured(callback);

                private static object? FirstArgument(
                    global::System.Collections.Generic.IReadOnlyList<object?> arguments) =>
                    arguments.Count == 0 ? null : arguments[0];
            }
        }
        """;
}
