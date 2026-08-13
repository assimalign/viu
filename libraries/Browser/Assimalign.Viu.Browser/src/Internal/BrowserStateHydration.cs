using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.State;

namespace Assimalign.Viu.Browser;

/// <summary>
/// Restores the application-composed state registry from the server JSON island after bridge
/// initialization and before the first component setup or render.
/// </summary>
[SupportedOSPlatform("browser")]
internal static class BrowserStateHydration
{
    internal const string StateIslandSelector = "script[data-viu-state]";

    internal static async Task InitializeAsync(
        IStateStoreRegistry registry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registry);
        await BrowserRuntime.EnsureBridgeAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        RestorePayload(registry, BrowserDomBridge.ConsumeTextContent);
    }

    internal static void RestorePayload(
        IStateStoreRegistry registry,
        Func<string, string> consumeTextContent)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(consumeTextContent);
        if (registry is not IStateStorePayloadRegistry payloadRegistry)
        {
            throw new InvalidOperationException(
                "A hydrating Browser application configured a state registry that does not "
                + "implement IStateStorePayloadRegistry. Use StateStoreRegistry or provide an "
                + "explicit AOT-safe payload registry implementation.");
        }

        string json = consumeTextContent(StateIslandSelector);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException(
                $"A hydrating Browser application with composed state requires the server state "
                + $"island selected by \"{StateIslandSelector}\" before mount.");
        }

        payloadRegistry.RestorePayload(StateStorePayload.Parse(json));
    }
}
