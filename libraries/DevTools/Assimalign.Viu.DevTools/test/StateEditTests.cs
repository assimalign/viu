using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.DevTools.Tests;

[Collection("DevTools runtime inspection")]
public sealed partial class StateEditTests
{
    [Theory]
    [InlineData("boolean", "true", true)]
    [InlineData("integer", "42", 42)]
    [InlineData("long", "9223372036854775807", long.MaxValue)]
    [InlineData("double", "2.5", 2.5)]
    [InlineData("decimal", "2.5", "2.5")]
    [InlineData("string", "\"changed\"", "changed")]
    [InlineData("string", "null", null)]
    public void Edit_ExactScalarTable_AssignsTypedReferences(string kind, string json, object? expected)
    {
        // [DVT-13]: no string-to-number conversion, narrowing, or untyped assignment.
        IReactiveReference reference = kind switch
        {
            "boolean" => Reactive.Reference(false),
            "integer" => Reactive.Reference(0),
            "long" => Reactive.Reference(0L),
            "double" => Reactive.Reference(0d),
            "decimal" => Reactive.Reference(0m),
            _ => Reactive.Reference("initial"),
        };
        Dictionary<string, object?> state = new() { ["member"] = reference };
        using JsonDocument document = JsonDocument.Parse(json);

        StateValueEditor.TryEdit(state, ["member", "value"], document.RootElement, out string? reason)
            .ShouldBeTrue();

        reason.ShouldBeNull();
        reference.Value.ShouldBe(kind == "decimal" ? 2.5m : expected);
    }

    [Theory]
    [InlineData("\"42\"")]
    [InlineData("2147483648")]
    [InlineData("1.5")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[]")]
    public void Edit_InvalidIntegerValue_RejectsWithoutEffectRuns(string json)
    {
        Reference<int> value = Reactive.Reference(1);
        int runs = 0;
        using ReactiveEffect effect = Reactive.Effect(() => { _ = value.Value; runs++; });
        using JsonDocument document = JsonDocument.Parse(json);

        StateValueEditor.TryEdit(
            new Dictionary<string, object?> { ["value"] = value }, ["value"],
            document.RootElement, out string? reason).ShouldBeFalse();

        reason.ShouldNotBeNull().ShouldContain("type or range");
        value.Value.ShouldBe(1);
        runs.ShouldBe(1);
    }

    [Fact]
    public void Edit_GeneratedMember_UsesNormalEqualityAndReactiveNotification()
    {
        // [DVT-13]: a generated write reuses the authored property's dependency.
        EditableModel model = new();
        int runs = 0;
        using ReactiveEffect effect = Reactive.Effect(() => { _ = model.Count; runs++; });
        Dictionary<string, object?> state = new() { ["model"] = model };
        using JsonDocument document = JsonDocument.Parse("7");

        StateValueEditor.TryEdit(state, ["model", "Count"], document.RootElement, out _).ShouldBeTrue();
        StateValueEditor.TryEdit(state, ["model", "Count"], document.RootElement, out _).ShouldBeTrue();

        model.Count.ShouldBe(7);
        runs.ShouldBe(2);
        SnapshotValueEncoder encoder = new(20);
        DevToolsValuePayload deferred = encoder.EncodeValue(model, 0, ["model"]);
        deferred.Expandable.ShouldBeTrue();
        deferred.Children.ShouldBeEmpty();
        encoder.EncodeValue(model, 1, ["model"]).Children
            .Single(member => member.Name == "Count").Value.DisplayValue.ShouldBe("7");
    }

    [Fact]
    public void Edit_ReadOnlyGeneratedAndComputedMembers_AreRejected()
    {
        int writes = 0;
        Computed<int> computed = Reactive.Computed(() => 1, _ => writes++);
        Dictionary<string, object?> state = new()
        {
            ["computed"] = computed,
            ["model"] = new ReadOnlyModel(),
            ["plain"] = 1,
            ["unsupported"] = Reactive.Reference(DateTime.UnixEpoch),
        };
        using JsonDocument document = JsonDocument.Parse("2");

        StateValueEditor.TryEdit(state, ["computed", "value"], document.RootElement, out string? reason)
            .ShouldBeFalse();
        reason.ShouldNotBeNull().ShouldContain("Computed");
        StateValueEditor.TryEdit(state, ["model", "Count"], document.RootElement, out reason)
            .ShouldBeFalse();
        reason.ShouldNotBeNull().ShouldContain("read-only");
        StateValueEditor.TryEdit(state, ["plain"], document.RootElement, out _).ShouldBeFalse();
        StateValueEditor.TryEdit(state, ["unsupported"], document.RootElement, out _).ShouldBeFalse();
        StateValueEditor.TryEdit(state, ["missing"], document.RootElement, out _).ShouldBeFalse();
        writes.ShouldBe(0);
    }

    [Fact]
    public void Edit_ThrowingSetter_ContainsFailureAndServesNextWrite()
    {
        Reference<int> good = Reactive.Reference(0);
        Dictionary<string, object?> state = new()
        {
            ["throwing"] = new ThrowingReference(), ["good"] = good,
        };
        using JsonDocument document = JsonDocument.Parse("2");

        StateValueEditor.TryEdit(state, ["throwing"], document.RootElement, out string? reason)
            .ShouldBeFalse();
        reason.ShouldNotBeNull().ShouldContain("setter failed");
        StateValueEditor.TryEdit(state, ["good"], document.RootElement, out _).ShouldBeTrue();
        good.Value.ShouldBe(2);
    }

    [Theory]
    [InlineData("object")]
    [InlineData("generated")]
    [InlineData("dictionary")]
    public void Edit_WritableComputedAncestor_RejectsBeforeEvaluatingOrWritingDescendants(string shape)
    {
        // [DVT-13]: generic computed classification rejects the whole derived path without reads.
        EditableModel model = new();
        Reference<int> count = Reactive.Reference(0);
        Dictionary<string, object?> dictionary = new() { ["count"] = count };
        int getterRuns = 0;
        int setterRuns = 0;
        IReactiveReference computed = shape switch
        {
            "object" => Reactive.Computed<object>(() => { getterRuns++; return model; }, _ => setterRuns++),
            "generated" => Reactive.Computed(() => { getterRuns++; return model; }, _ => setterRuns++),
            _ => Reactive.Computed(() => { getterRuns++; return dictionary; }, _ => setterRuns++),
        };
        int effectRuns = 0;
        using ReactiveEffect effect = Reactive.Effect(() => { _ = model.Count; _ = count.Value; effectRuns++; });
        Dictionary<string, object?> state = new() { ["computed"] = computed };
        using JsonDocument document = JsonDocument.Parse("7");
        string member = shape == "dictionary" ? "count" : "Count";

        StateValueEditor.TryEdit(state, ["computed", "value", member], document.RootElement, out string? reason)
            .ShouldBeFalse();

        reason.ShouldBe("Computed references are not editable.");
        getterRuns.ShouldBe(0);
        setterRuns.ShouldBe(0);
        effectRuns.ShouldBe(1);
        model.Count.ShouldBe(0);
        count.Value.ShouldBe(0);
    }

    [Theory]
    [InlineData("object")]
    [InlineData("generated")]
    [InlineData("dictionary")]
    public void Edit_MutableObjectReferenceAncestor_PreservesOrdinaryDescendantEditing(string shape)
    {
        EditableModel model = new();
        Reference<int> count = Reactive.Reference(0);
        Dictionary<string, object?> dictionary = new() { ["count"] = count };
        IReactiveReference reference = shape switch
        {
            "object" => Reactive.Reference<object>(model),
            "generated" => Reactive.Reference(model),
            _ => Reactive.Reference(dictionary),
        };
        int effectRuns = 0;
        using ReactiveEffect effect = Reactive.Effect(() => { _ = model.Count; _ = count.Value; effectRuns++; });
        Dictionary<string, object?> state = new() { ["reference"] = reference };
        using JsonDocument document = JsonDocument.Parse("7");
        string member = shape == "dictionary" ? "count" : "Count";

        StateValueEditor.TryEdit(state, ["reference", "value", member], document.RootElement, out string? reason)
            .ShouldBeTrue();

        reason.ShouldBeNull();
        effectRuns.ShouldBe(2);
        (shape == "dictionary" ? count.Value : model.Count).ShouldBe(7);
    }

    [Theory]
    [InlineData("postMessage")]
    [InlineData("webSocket")]
    public async Task Transport_EditCorpus_ChangesRenderedStateAndReliablyRejectsParameters(string kind)
    {
        // [DVT-13]: both concrete transports deliver identical edit behavior and control responses.
        Scheduler.Reset();
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        ITransportHarness harness = TransportHarness.Create(kind);
        await using DevToolsSession session = new(harness.Transport, new DevToolsSessionOptions { BufferCapacity = 1 });
        await session.StartAsync();
        await harness.ReceiveAsync("{\"version\":1,\"type\":\"handshake.request\",\"payload\":{\"supportedVersions\":[1]}}");
        await WaitForAsync(harness, pump, "handshake.response", 1);

        Reference<int> count = Reactive.Reference(1);
        int renders = 0;
        ComponentRegistration registration = ComponentRegistration.Define(
            "Editable", new ComponentContract(displayName: "Editable"), context =>
            {
                context.Expose(new Dictionary<string, object?> { ["count"] = count });
                return _ => { renders++; return new TextNode(count.Value.ToString()); };
            });
        ComponentFactory factory = new();
        factory.Register(registration);
        ComponentNode root = new(registration.Reference);
        ApplicationContext application = new(new ApplicationOptions { Components = factory, RootComponent = root });
        using TestRenderer renderer = new();
        TestElement container = renderer.CreateContainer();
        renderer.Render(root, container, application);
        await harness.ReceiveAsync("{\"version\":1,\"type\":\"tree.snapshot.request\",\"payload\":{}}");
        await WaitForAsync(harness, pump, "tree.snapshot", 1);
        int identifier = Messages(harness, "tree.snapshot").Single().Payload
            .Deserialize(DevToolsJsonSerializerContext.Default.ComponentTreeSnapshotPayload)!.Roots[0].Identifier;

        using JsonDocument value = JsonDocument.Parse("9");
        await harness.ReceiveAsync(ProtocolCodec.SerializeBatch(
        [
            ProtocolCodec.CreateEnvelope("state.edit.request", new StateEditRequestPayload(
                identifier, "state", ["count", "value"], value.RootElement),
                DevToolsJsonSerializerContext.Default.StateEditRequestPayload),
            ProtocolCodec.CreateEnvelope("state.edit.request", new StateEditRequestPayload(
                identifier, "parameters", ["count"], value.RootElement),
                DevToolsJsonSerializerContext.Default.StateEditRequestPayload),
        ]));
        await WaitForAsync(harness, pump, "state.edit.response", 2);

        StateEditResponsePayload[] responses = Messages(harness, "state.edit.response")
            .Select(message => message.Payload.Deserialize(DevToolsJsonSerializerContext.Default.StateEditResponsePayload)!).ToArray();
        responses[0].Accepted.ShouldBeTrue();
        responses[1].Accepted.ShouldBeFalse();
        responses[1].Reason.ShouldBe("Component parameters are read-only.");
        count.Value.ShouldBe(9);
        renders.ShouldBe(2);
        container.Children.OfType<TestText>().Single().Text.ShouldBe("9");

        renderer.Render(null, container);
        await harness.ReceiveAsync(ProtocolCodec.SerializeBatch(
        [
            ProtocolCodec.CreateEnvelope("state.edit.request", new StateEditRequestPayload(
                identifier, "state", ["count"], value.RootElement),
                DevToolsJsonSerializerContext.Default.StateEditRequestPayload),
        ]));
        await WaitForAsync(harness, pump, "state.edit.response", 3);
        Messages(harness, "state.edit.response").Last().Payload
            .Deserialize(DevToolsJsonSerializerContext.Default.StateEditResponsePayload)!.Reason
            .ShouldBe("The component is no longer mounted.");
        Scheduler.Reset();
    }

    private static IEnumerable<ProtocolEnvelope> Messages(ITransportHarness harness, string type) =>
        harness.SentFrames.SelectMany(frame => ProtocolCodec.DeserializeBatch(frame)!.Messages)
            .Where(message => message.Type == type);

    private static async Task WaitForAsync(ITransportHarness harness, TestSchedulerPump pump, string type, int count)
    {
        for (int attempt = 0; attempt < 400; attempt++)
        {
            pump.RunUntilIdle();
            if (Messages(harness, type).Count() >= count)
            {
                return;
            }
            await Task.Delay(5);
        }
        throw new TimeoutException($"Missing {type} response.");
    }

    [Reactive]
    private sealed partial class EditableModel
    {
        public partial int Count { get; set; }
    }

    [Reactive(ReadOnly = true)]
    private sealed partial class ReadOnlyModel
    {
        public partial int Count { get; set; }
    }

    private sealed class ThrowingReference : IReactiveReference<int>
    {
        public int Value { get => 1; set => throw new InvalidOperationException("expected"); }
        object? IReactiveReference.Value => Value;
    }
}
