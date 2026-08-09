using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using Shouldly;
using Xunit;

using Assimalign.Viu.State;

namespace Assimalign.Viu.Browser.Tests;

public sealed class BrowserStateHydrationTests
{
    private static readonly StateStoreDefinition<BrowserPayloadStore> StoreDefinition =
        StateStores.Define(
            "browser-state",
            static () => new BrowserPayloadStore(),
            new StateStoreJsonSerializer<BrowserPayloadStore, BrowserPayloadState>(
                static stateStore => stateStore.State,
                static (stateStore, state) => stateStore.State = state,
                BrowserStateHydrationJsonContext.Default.BrowserPayloadState));

    [Fact]
    public void RestorePayload_StoreResolvedDuringMount_UsesServerStateInsteadOfDefaults()
    {
        StateStorePayload payload = CreatePayload("server-value");
        using IStateStoreRegistry clientRegistry = StateStores.CreateRegistry();
        string? observedSelector = null;

        BrowserStateHydration.RestorePayload(
            clientRegistry,
            selector =>
            {
                observedSelector = selector;
                return payload.Json;
            });

        observedSelector.ShouldBe(BrowserStateHydration.StateIslandSelector);
        StoreDefinition.Use(clientRegistry).State.Message.ShouldBe("server-value");
    }

    [Fact]
    public void RestorePayload_StoreResolvedBeforeMount_IsUpdatedBeforeFirstRender()
    {
        StateStorePayload payload = CreatePayload("server-value");
        using IStateStoreRegistry clientRegistry = StateStores.CreateRegistry();
        BrowserPayloadStore stateStore = StoreDefinition.Use(clientRegistry);
        stateStore.State.Message.ShouldBe("client-default");

        BrowserStateHydration.RestorePayload(
            clientRegistry,
            _ => payload.Json);

        stateStore.State.Message.ShouldBe("server-value");
    }

    [Fact]
    public void RestorePayload_StateIslandInsideMountContainer_IsConsumedBeforeHydration()
    {
        StateStorePayload payload = CreatePayload("server-value");
        using IStateStoreRegistry clientRegistry = StateStores.CreateRegistry();
        List<string> mountChildren = ["server-root", "state-island"];

        BrowserStateHydration.RestorePayload(
            clientRegistry,
            selector =>
            {
                selector.ShouldBe(BrowserStateHydration.StateIslandSelector);
                mountChildren.Remove("state-island").ShouldBeTrue();
                return payload.Json;
            });

        mountChildren.ShouldBe(["server-root"]);
        StoreDefinition.Use(clientRegistry).State.Message.ShouldBe("server-value");
    }

    [Fact]
    public void RestorePayload_MissingIsland_ThrowsBeforeAnyStoreIsResolved()
    {
        using IStateStoreRegistry clientRegistry = StateStores.CreateRegistry();

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => BrowserStateHydration.RestorePayload(clientRegistry, _ => string.Empty));

        exception.Message.ShouldContain(BrowserStateHydration.StateIslandSelector);
        clientRegistry.Count.ShouldBe(0);
    }

    private static StateStorePayload CreatePayload(string message)
    {
        using IStateStoreRegistry serverRegistry = StateStores.CreateRegistry();
        StoreDefinition.Use(serverRegistry).State.Message = message;
        return ((IStateStorePayloadRegistry)serverRegistry).CapturePayload();
    }
}

internal sealed class BrowserPayloadStore
{
    internal BrowserPayloadState State { get; set; } = new()
    {
        Message = "client-default",
    };
}

internal sealed class BrowserPayloadState
{
    public string Message { get; set; } = string.Empty;
}

[JsonSerializable(typeof(BrowserPayloadState))]
internal sealed partial class BrowserStateHydrationJsonContext : JsonSerializerContext
{
}
