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
/// expression down with it: a maybe-reference expression can unwrap through this glue, including a
/// <c>v-for</c> source, so its alias and member accesses need the same helper type in both hosts.
/// Plain collection members retain their declared type through direct access (<c>[SFC-6]</c>).
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
                internal static global::System.Action AdaptHandler(global::System.Action handler) => handler;

                internal static global::System.Action<TEvent> AdaptHandler<TEvent>(global::System.Action<TEvent> handler) => handler;

                internal static global::System.Func<global::System.Threading.Tasks.Task> AdaptHandler(
                    global::System.Func<global::System.Threading.Tasks.Task> handler) => handler;

                internal static global::System.Func<TEvent, global::System.Threading.Tasks.Task> AdaptHandler<TEvent>(
                    global::System.Func<TEvent, global::System.Threading.Tasks.Task> handler) => handler;

                internal static global::System.Func<global::System.Threading.Tasks.Task> AdaptHandler<TResult>(
                    global::System.Func<global::System.Threading.Tasks.Task<TResult>> handler) => handler;

                internal static global::System.Func<TEvent, global::System.Threading.Tasks.Task> AdaptHandler<TEvent, TResult>(
                    global::System.Func<TEvent, global::System.Threading.Tasks.Task<TResult>> handler) => handler;

                internal static global::System.Action AdaptHandler<TResult>(global::System.Func<TResult> handler)
                    => () => { handler(); };

                internal static global::System.Action<TEvent> AdaptHandler<TEvent, TResult>(global::System.Func<TEvent, TResult> handler)
                    => value => { handler(value); };

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

                // Portable native events retain their host-neutral delegate type across assembly
                // boundaries. Wrapping these in object delegates would require a host-specific shim.
                internal static global::System.Action<global::Assimalign.Viu.Components.IElementEvent> Handler(
                    global::System.Action<global::Assimalign.Viu.Components.IElementEvent> handler)
                {
                    global::System.ArgumentNullException.ThrowIfNull(handler);
                    return handler;
                }

                internal static global::System.Func<global::Assimalign.Viu.Components.IElementEvent, global::System.Threading.Tasks.Task> Handler(
                    global::System.Func<global::Assimalign.Viu.Components.IElementEvent, global::System.Threading.Tasks.Task> handler)
                {
                    global::System.ArgumentNullException.ThrowIfNull(handler);
                    return handler;
                }

                internal static global::System.Func<TEvent, global::System.Threading.Tasks.Task> Handler<TEvent>(
                    global::System.Func<TEvent, global::System.Threading.Tasks.Task> handler)
                {
                    global::System.ArgumentNullException.ThrowIfNull(handler);
                    return handler;
                }

                internal static global::System.Action<TEvent> Handler<TEvent>(global::System.Action<TEvent> handler)
                {
                    global::System.ArgumentNullException.ThrowIfNull(handler);
                    return handler;
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
                    global::System.Action handler, global::Assimalign.Viu.Components.ComponentLifecycle lifecycle)
                {
                    global::System.ArgumentNullException.ThrowIfNull(handler);
                    return arguments => handler();
                }

                internal static global::Assimalign.Viu.Components.ComponentEventListener ComponentListener(
                    global::System.Action<object?> handler, global::Assimalign.Viu.Components.ComponentLifecycle lifecycle)
                {
                    global::System.ArgumentNullException.ThrowIfNull(handler);
                    return arguments => handler(FirstArgument(arguments));
                }

                internal static global::Assimalign.Viu.Components.ComponentEventListener ComponentListener(
                    global::System.Func<object?> handler, global::Assimalign.Viu.Components.ComponentLifecycle lifecycle)
                {
                    global::System.ArgumentNullException.ThrowIfNull(handler);
                    return arguments => handler();
                }

                internal static global::Assimalign.Viu.Components.ComponentEventListener ComponentListener(
                    global::System.Func<object?, object?> handler, global::Assimalign.Viu.Components.ComponentLifecycle lifecycle)
                {
                    global::System.ArgumentNullException.ThrowIfNull(handler);
                    return arguments => handler(FirstArgument(arguments));
                }

                internal static global::Assimalign.Viu.Components.ComponentEventListener ComponentListener<TEvent>(
                    global::System.Action<TEvent> handler, global::Assimalign.Viu.Components.ComponentLifecycle lifecycle)
                {
                    global::System.ArgumentNullException.ThrowIfNull(handler);
                    return arguments => handler((TEvent)FirstArgument(arguments)!);
                }

                internal static global::Assimalign.Viu.Components.ComponentEventListener ComponentListener(
                    global::System.Func<global::System.Threading.Tasks.Task> handler,
                    global::Assimalign.Viu.Components.ComponentLifecycle lifecycle)
                    => arguments => lifecycle.ObserveTask(handler(), "component event listener");

                internal static global::Assimalign.Viu.Components.ComponentEventListener ComponentListener<TEvent>(
                    global::System.Func<TEvent, global::System.Threading.Tasks.Task> handler,
                    global::Assimalign.Viu.Components.ComponentLifecycle lifecycle)
                    => arguments => lifecycle.ObserveTask(handler((TEvent)FirstArgument(arguments)!), "component event listener");

                // The second argument supplies the assignment type without reflection. Known script
                // declarations use default(T), avoiding an additional target getter read. Overloads convert scalars;
                // the generic fallback preserves object and explicitly typed component payloads.
                internal static TValue ModelValue<TValue>(object? value, TValue current)
                    => (TValue)value!;

                internal static string ModelValue(object? value, string? current)
                    => global::System.Convert.ToString(value, global::System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;

                internal static bool ModelValue(object? value, bool current)
                    => global::System.Convert.ToBoolean(value, global::System.Globalization.CultureInfo.InvariantCulture);

                internal static bool? ModelValue(object? value, bool? current)
                    => value is null or "" ? null : global::System.Convert.ToBoolean(value, global::System.Globalization.CultureInfo.InvariantCulture);

                internal static byte ModelValue(object? value, byte current)
                    => global::System.Convert.ToByte(value, global::System.Globalization.CultureInfo.InvariantCulture);

                internal static byte? ModelValue(object? value, byte? current)
                    => value is null or "" ? null : global::System.Convert.ToByte(value, global::System.Globalization.CultureInfo.InvariantCulture);

                internal static sbyte ModelValue(object? value, sbyte current)
                    => global::System.Convert.ToSByte(value, global::System.Globalization.CultureInfo.InvariantCulture);

                internal static sbyte? ModelValue(object? value, sbyte? current)
                    => value is null or "" ? null : global::System.Convert.ToSByte(value, global::System.Globalization.CultureInfo.InvariantCulture);

                internal static short ModelValue(object? value, short current)
                    => global::System.Convert.ToInt16(value, global::System.Globalization.CultureInfo.InvariantCulture);

                internal static short? ModelValue(object? value, short? current)
                    => value is null or "" ? null : global::System.Convert.ToInt16(value, global::System.Globalization.CultureInfo.InvariantCulture);

                internal static ushort ModelValue(object? value, ushort current)
                    => global::System.Convert.ToUInt16(value, global::System.Globalization.CultureInfo.InvariantCulture);

                internal static ushort? ModelValue(object? value, ushort? current)
                    => value is null or "" ? null : global::System.Convert.ToUInt16(value, global::System.Globalization.CultureInfo.InvariantCulture);

                internal static int ModelValue(object? value, int current)
                    => global::System.Convert.ToInt32(value, global::System.Globalization.CultureInfo.InvariantCulture);

                internal static int? ModelValue(object? value, int? current)
                    => value is null or "" ? null : global::System.Convert.ToInt32(value, global::System.Globalization.CultureInfo.InvariantCulture);

                internal static uint ModelValue(object? value, uint current)
                    => global::System.Convert.ToUInt32(value, global::System.Globalization.CultureInfo.InvariantCulture);

                internal static uint? ModelValue(object? value, uint? current)
                    => value is null or "" ? null : global::System.Convert.ToUInt32(value, global::System.Globalization.CultureInfo.InvariantCulture);

                internal static long ModelValue(object? value, long current)
                    => global::System.Convert.ToInt64(value, global::System.Globalization.CultureInfo.InvariantCulture);

                internal static long? ModelValue(object? value, long? current)
                    => value is null or "" ? null : global::System.Convert.ToInt64(value, global::System.Globalization.CultureInfo.InvariantCulture);

                internal static ulong ModelValue(object? value, ulong current)
                    => global::System.Convert.ToUInt64(value, global::System.Globalization.CultureInfo.InvariantCulture);

                internal static ulong? ModelValue(object? value, ulong? current)
                    => value is null or "" ? null : global::System.Convert.ToUInt64(value, global::System.Globalization.CultureInfo.InvariantCulture);

                internal static float ModelValue(object? value, float current)
                    => global::System.Convert.ToSingle(value, global::System.Globalization.CultureInfo.InvariantCulture);

                internal static float? ModelValue(object? value, float? current)
                    => value is null or "" ? null : global::System.Convert.ToSingle(value, global::System.Globalization.CultureInfo.InvariantCulture);

                internal static double ModelValue(object? value, double current)
                    => global::System.Convert.ToDouble(value, global::System.Globalization.CultureInfo.InvariantCulture);

                internal static double? ModelValue(object? value, double? current)
                    => value is null or "" ? null : global::System.Convert.ToDouble(value, global::System.Globalization.CultureInfo.InvariantCulture);

                internal static decimal ModelValue(object? value, decimal current)
                    => global::System.Convert.ToDecimal(value, global::System.Globalization.CultureInfo.InvariantCulture);

                internal static decimal? ModelValue(object? value, decimal? current)
                    => value is null or "" ? null : global::System.Convert.ToDecimal(value, global::System.Globalization.CultureInfo.InvariantCulture);

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
