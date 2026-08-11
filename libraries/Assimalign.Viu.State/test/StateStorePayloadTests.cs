using System;
using System.Text.Json;
using System.Text.Json.Serialization;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.State.Tests;

public sealed class StateStorePayloadTests
{
    [Fact]
    public void CapturePayload_OnlyOneDefinitionMaterialized_ContainsOnlyThatStoreKey()
    {
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry();
        StateStoreDefinition<PayloadStore> used = CreateDefinition("used");
        _ = CreateDefinition("unused");
        registry.GetOrCreate(used).State.Count = 42;

        StateStorePayload payload = registry.CapturePayload();

        payload.Version.ShouldBe(StateStorePayload.CurrentVersion);
        payload.StoreKeys.ShouldBe(["used"]);
        payload.Json.ShouldContain("\"stores\":{\"used\":");
        payload.Json.ShouldNotContain("unused");
    }

    [Fact]
    public void CapturePayload_MaterializedDefinitionHasNoSerializer_ThrowsActionableError()
    {
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry();
        StateStoreDefinition<PayloadStore> definition = new(
            "missing-serializer",
            static _ => new PayloadStore());
        registry.GetOrCreate(definition);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => registry.CapturePayload());

        exception.Message.ShouldContain("missing-serializer");
        exception.Message.ShouldContain("IStateStoreSerializer<TStore>");
    }

    [Fact]
    public void RestorePayload_StoreCreatedAfterRestore_InitializesBeforeResolutionReturns()
    {
        StateStoreDefinition<PayloadStore> definition = CreateDefinition("counter");
        StateStorePayload payload;
        using (StateStoreRegistry serverRegistry = StateStoreTestSupport.CreateRegistry())
        {
            PayloadStore serverStore = serverRegistry.GetOrCreate(definition);
            serverStore.State.Count = 37;
            serverStore.State.Message = "server";
            payload = serverRegistry.CapturePayload();
        }

        using StateStoreRegistry clientRegistry = StateStoreTestSupport.CreateRegistry();
        clientRegistry.RestorePayload(StateStorePayload.Parse(payload.Json));

        PayloadStore clientStore = clientRegistry.GetOrCreate(definition);

        clientStore.State.Count.ShouldBe(37);
        clientStore.State.Message.ShouldBe("server");
    }

    [Fact]
    public void RestorePayload_StoreAlreadyMaterialized_RestoresImmediately()
    {
        StateStoreDefinition<PayloadStore> definition = CreateDefinition("counter");
        StateStorePayload payload;
        using (StateStoreRegistry serverRegistry = StateStoreTestSupport.CreateRegistry())
        {
            serverRegistry.GetOrCreate(definition).State.Count = 81;
            payload = serverRegistry.CapturePayload();
        }

        using StateStoreRegistry clientRegistry = StateStoreTestSupport.CreateRegistry();
        PayloadStore clientStore = clientRegistry.GetOrCreate(definition);
        clientStore.State.Count.ShouldBe(0);

        clientRegistry.RestorePayload(payload);

        clientStore.State.Count.ShouldBe(81);
    }

    [Fact]
    public void CaptureAndRestore_HtmlSensitiveState_ProducesSafeIslandJsonAndRoundTrips()
    {
        const string message = "</script>\u2028\u2029<&>";
        StateStoreDefinition<PayloadStore> definition = CreateDefinition("unsafe");
        StateStorePayload payload;
        using (StateStoreRegistry serverRegistry = StateStoreTestSupport.CreateRegistry())
        {
            serverRegistry.GetOrCreate(definition).State.Message = message;
            payload = serverRegistry.CapturePayload();
        }

        payload.Json.ShouldNotContain("</script>");
        payload.Json.ShouldNotContain("<");
        payload.Json.ShouldNotContain(">");
        payload.Json.ShouldNotContain("&");
        payload.Json.ShouldNotContain("\u2028");
        payload.Json.ShouldNotContain("\u2029");
        payload.Json.ShouldContain("\\u003C");
        payload.Json.ShouldContain("\\u2028");
        payload.Json.ShouldContain("\\u2029");

        using StateStoreRegistry clientRegistry = StateStoreTestSupport.CreateRegistry();
        clientRegistry.RestorePayload(StateStorePayload.Parse(payload.Json));
        clientRegistry.GetOrCreate(definition).State.Message.ShouldBe(message);
    }

    [Fact]
    public void Registries_DifferentStateValues_CaptureIndependentPayloads()
    {
        StateStoreDefinition<PayloadStore> definition = CreateDefinition("request");
        using StateStoreRegistry firstRegistry = StateStoreTestSupport.CreateRegistry();
        using StateStoreRegistry secondRegistry = StateStoreTestSupport.CreateRegistry();
        firstRegistry.GetOrCreate(definition).State.Message = "first";
        secondRegistry.GetOrCreate(definition).State.Message = "second";

        StateStorePayload firstPayload = firstRegistry.CapturePayload();
        StateStorePayload secondPayload = secondRegistry.CapturePayload();

        firstPayload.Json.ShouldContain("first");
        firstPayload.Json.ShouldNotContain("second");
        secondPayload.Json.ShouldContain("second");
        secondPayload.Json.ShouldNotContain("first");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"version\":2,\"stores\":{}}")]
    [InlineData("{\"version\":1,\"stores\":{},\"extra\":true}")]
    [InlineData("{\"version\":1,\"stores\":{\"same\":{},\"same\":{}}}")]
    public void Parse_InvalidSchema_ThrowsJsonException(string json)
    {
        Should.Throw<JsonException>(() => StateStorePayload.Parse(json));
    }

    private static StateStoreDefinition<PayloadStore> CreateDefinition(string key) =>
        StateStores.Define(
            key,
            static () => new PayloadStore(),
            new StateStoreJsonSerializer<PayloadStore, PayloadState>(
                static stateStore => stateStore.State,
                static (stateStore, state) => stateStore.State = state,
                StateStorePayloadJsonContext.Default.PayloadState));
}

internal sealed class PayloadStore
{
    internal PayloadState State { get; set; } = new();
}

internal sealed class PayloadState
{
    public int Count { get; set; }

    public string Message { get; set; } = string.Empty;
}

[JsonSerializable(typeof(PayloadState))]
internal sealed partial class StateStorePayloadJsonContext : JsonSerializerContext
{
}
