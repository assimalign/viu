using System;

using Assimalign.Viu.State;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Shares the post-traversal state capture and island emission contract between runtime-tree and
/// compiler-produced server render bodies.
/// </summary>
internal static class ServerRenderStatePayload
{
    internal static void PrepareContext(SsrContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.State = null;
    }

    internal static void CaptureAndAppend(SsrRenderState renderState)
    {
        ArgumentNullException.ThrowIfNull(renderState);
        if (renderState.Application.State is IStateStorePayloadRegistry payloadRegistry)
        {
            renderState.Context.State = payloadRegistry.CapturePayload();
            renderState.Push(
                SsrStateIsland.CreateMarkup(renderState.Context.State.Json));
        }
        else if (renderState.Application.State is { Count: > 0 })
        {
            throw new InvalidOperationException(
                "The configured state registry materialized stores during server rendering but "
                + "does not implement IStateStorePayloadRegistry. Use StateStoreRegistry or "
                + "provide an explicit AOT-safe payload registry implementation.");
        }
    }
}
