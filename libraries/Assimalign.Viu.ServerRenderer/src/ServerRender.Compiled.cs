using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu;

namespace Assimalign.Viu.ServerRenderer;

/// <content>
/// Provides the invocation and normalization seam for compiler-produced server render bodies.
/// </content>
public static partial class ServerRender
{
    /// <summary>Renders one compiler-produced server body to a completed HTML string.</summary>
    /// <param name="application">The immutable per-render application composition.</param>
    /// <param name="render">The statically generated render body.</param>
    /// <param name="context">The per-render state and teleport context, or null to create one.</param>
    /// <param name="cancellationToken">Cancellation for direct writes and virtual-tree fallbacks.</param>
    /// <returns>The completed WHATWG HTML serialization.</returns>
    /// <remarks>
    /// The compiled body owns root markup production; <paramref name="application"/> supplies the
    /// borrowed component, service, state, and diagnostic composition used by fallback regions.
    /// The final flush, teleport resolution, and fresh logical execution state are identical to
    /// tree traversal. Specified by <c>[EXE-1]</c>, <c>[SSR-COMPILE-4]</c>, <c>[SSR-1]</c>, and
    /// <c>[SSR-7]</c>.
    /// </remarks>
    public static async Task<string> RenderCompiledToStringAsync(
        ServerRenderApplication application,
        CompiledServerRender render,
        SsrContext? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(render);

        SsrWriter writer = new();
        await RenderCompiledCoreAsync(
            application,
            render,
            writer,
            context ?? new SsrContext(),
            cancellationToken).ConfigureAwait(false);
        return writer.ToStringResult();
    }

    /// <summary>
    /// Streams one compiler-produced server body and flushes its final direct-write chunk.
    /// </summary>
    /// <param name="application">The immutable per-render application composition.</param>
    /// <param name="render">The statically generated render body.</param>
    /// <param name="writer">The externally owned output destination.</param>
    /// <param name="context">The per-render state and teleport context, or null to create one.</param>
    /// <param name="cancellationToken">Cancellation for direct writes, fallbacks, and flushing.</param>
    /// <returns>A task completing after the destination's final flush.</returns>
    /// <remarks>
    /// Component fallbacks keep their ordinary subtree flush boundaries; direct regions are drained by
    /// the final flush and fresh logical execution state. The destination controls backpressure
    /// under <c>[SSR-1]</c>. Specified by <c>[EXE-1]</c>, <c>[SSR-COMPILE-4]</c>, <c>[SSR-1]</c>,
    /// and <c>[SSR-7]</c>.
    /// </remarks>
    public static Task RenderCompiledToStreamAsync(
        ServerRenderApplication application,
        CompiledServerRender render,
        TextWriter writer,
        SsrContext? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(writer);
        return RenderCompiledCoreAsync(
            application,
            render,
            new SsrWriter(writer),
            context ?? new SsrContext(),
            cancellationToken);
    }

    /// <summary>Tests a value using the truth contract shared by class, style, and server directives.</summary>
    /// <param name="value">The value to test.</param>
    /// <returns>Whether the value participates as truthy server-render input.</returns>
    /// <remarks>
    /// Compiler-produced <c>v-show</c> branches use this helper so their style output follows the same
    /// scalar truth rules as runtime normalization. Specified by <c>[SSR-COMPILE-3]</c> and
    /// <c>[SSR-6]</c>.
    /// </remarks>
    public static bool IsTruthy(object? value) => StyleAndClassNormalization.IsTruthy(value);

    /// <summary>Tests whether one candidate is selected by a scalar or collection model value.</summary>
    /// <param name="modelValue">The current model value.</param>
    /// <param name="candidate">The input or option value being rendered.</param>
    /// <param name="booleanModelIsSelection">
    /// Whether a boolean model selects directly, as it does for a checkbox.
    /// </param>
    /// <returns>
    /// For a boolean checkbox model, its boolean value; for a non-string enumerable, whether any item
    /// equals <paramref name="candidate"/>; otherwise whether the scalar values are equal.
    /// </returns>
    /// <remarks>
    /// This explicit, reflection-free comparison is the compile-time server-rendering contract for
    /// <c>checked</c> and <c>selected</c> attributes. Specified by <c>[SSR-COMPILE-3]</c> and
    /// <c>[SSR-6]</c>.
    /// </remarks>
    public static bool IsModelValueSelected(
        object? modelValue,
        object? candidate,
        bool booleanModelIsSelection = false)
    {
        if (booleanModelIsSelection && modelValue is bool booleanValue)
        {
            return booleanValue;
        }

        if (modelValue is IEnumerable values && modelValue is not string)
        {
            foreach (object? value in values)
            {
                if (Equals(value, candidate))
                {
                    return true;
                }
            }

            return false;
        }

        return Equals(modelValue, candidate);
    }

    private static async Task RenderCompiledCoreAsync(
        ServerRenderApplication application,
        CompiledServerRender render,
        SsrWriter writer,
        SsrContext context,
        CancellationToken cancellationToken)
    {
        using IDisposable executionIsolation = CoreExecutionIsolation.Enter();
        cancellationToken.ThrowIfCancellationRequested();
        IApplicationContext applicationContext = application.Context;
        ServerRenderStatePayload.PrepareContext(context);
        ComponentHost componentHost = new(
            new ComponentRuntimeOptions(
                applicationContext.Components,
                new ApplicationWatchScheduler(),
                applicationContext.Services,
                applicationContext.ErrorHandler,
                applicationContext.WarnHandler,
                applicationContext.EventObserver));
        SsrRenderState state = new(
            writer,
            context,
            applicationContext,
            componentHost,
            cancellationToken);

        await render(state).ConfigureAwait(false);
        ServerRenderStatePayload.CaptureAndAppend(state);
        await state.FlushAsync().ConfigureAwait(false);
        context.ResolveTeleports();
    }
}
