using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.State;

namespace Assimalign.Viu.ServerRenderer.Tests;

public sealed class ServerRendererStateHydrationTests
{
    private static readonly StateStoreDefinition<SsrPayloadStore> StoreDefinition =
        StateStores.Define(
            "request-state",
            static context => new SsrPayloadStore
            {
                State = new SsrPayloadState
                {
                    Message = (string?)context.Services?.GetService(typeof(string))
                        ?? "server-default",
                },
            },
            new StateStoreJsonSerializer<SsrPayloadStore, SsrPayloadState>(
                static stateStore => stateStore.State,
                static (stateStore, state) => stateStore.State = state,
                ServerRendererStateJsonContext.Default.SsrPayloadState));

    [Fact]
    public async Task RenderToStringAsync_MaterializedState_EmitsIslandAndRestoresBeforeFirstRender()
    {
        ComponentFactory components = CreateComponents();
        using StateStoreRegistry serverRegistry = CreateRegistry("server-value");
        SsrContext serverContext = new();

        string serverHtml = await ServerRenderer.RenderToStringAsync(
            CreateApplication(components, serverRegistry),
            serverContext);

        (string serverMarkup, string islandJson) = SplitStateIsland(serverHtml);
        serverMarkup.ShouldBe("<p>server-value</p>");
        serverContext.State.ShouldNotBeNull();
        serverContext.State.StoreKeys.ShouldBe(["request-state"]);
        islandJson.ShouldBe(serverContext.State.Json);

        using StateStoreRegistry clientRegistry = CreateRegistry("client-default");
        clientRegistry.RestorePayload(StateStorePayload.Parse(islandJson));
        string firstClientHtml = await ServerRenderer.RenderToStringAsync(
            CreateApplication(components, clientRegistry));
        (string firstClientMarkup, _) = SplitStateIsland(firstClientHtml);

        firstClientMarkup.ShouldBe(serverMarkup);
        clientRegistry.GetOrCreate(StoreDefinition).State.Message.ShouldBe("server-value");
    }

    [Fact]
    public async Task RenderToStringAsync_HtmlSensitiveState_IslandIsSafeAndRoundTrips()
    {
        const string dangerous = "</script>\u2028\u2029<&>";
        using StateStoreRegistry serverRegistry = CreateRegistry(dangerous);

        string html = await ServerRenderer.RenderToStringAsync(
            CreateApplication(CreateComponents(), serverRegistry));
        (_, string islandJson) = SplitStateIsland(html);

        islandJson.ShouldNotContain("</script>");
        islandJson.ShouldNotContain("<");
        islandJson.ShouldNotContain(">");
        islandJson.ShouldNotContain("&");
        islandJson.ShouldNotContain("\u2028");
        islandJson.ShouldNotContain("\u2029");

        using StateStoreRegistry clientRegistry = CreateRegistry("client-default");
        clientRegistry.RestorePayload(StateStorePayload.Parse(islandJson));
        clientRegistry.GetOrCreate(StoreDefinition).State.Message.ShouldBe(dangerous);
    }

    [Fact]
    public async Task RenderToStringAsync_ConcurrentRequestRegistries_DoNotBleedPayloadState()
    {
        ConcurrentRenderBarrier barrier = new();
        ComponentFactory components = CreateComponents(barrier);
        using StateStoreRegistry firstRegistry = CreateRegistry("first-request");
        using StateStoreRegistry secondRegistry = CreateRegistry("second-request");
        SsrContext firstContext = new();
        SsrContext secondContext = new();

        Task<string> firstRender = ServerRenderer.RenderToStringAsync(
            CreateApplication(components, firstRegistry),
            firstContext);
        Task<string> secondRender = ServerRenderer.RenderToStringAsync(
            CreateApplication(components, secondRegistry),
            secondContext);
        string[] outputs = await Task.WhenAll(firstRender, secondRender);

        outputs[0].ShouldContain("first-request");
        outputs[0].ShouldNotContain("second-request");
        outputs[1].ShouldContain("second-request");
        outputs[1].ShouldNotContain("first-request");
        firstContext.State!.Json.ShouldContain("first-request");
        firstContext.State.Json.ShouldNotContain("second-request");
        secondContext.State!.Json.ShouldContain("second-request");
        secondContext.State.Json.ShouldNotContain("first-request");
    }

    private static ComponentFactory CreateComponents(
        ConcurrentRenderBarrier? barrier = null)
    {
        ComponentFactory components = new();
        components.Register(
            new ComponentRegistration(
                ComponentReference.ForName("state-payload"),
                new ComponentContract(),
                _ => new SsrStateComponent(StoreDefinition, barrier)));
        return components;
    }

    private static ServerRenderApplication CreateApplication(
        IComponentFactory components,
        IStateStoreRegistry registry) =>
        ServerRenderApplication
            .CreateBuilder(
                new ComponentNode(ComponentReference.ForName("state-payload")),
                components)
            .ConfigureApplication(options => options.State = registry)
            .Build();

    private static StateStoreRegistry CreateRegistry(string message) =>
        new(
            new StringServiceProvider(message),
            new ReactiveEffectScopeFactory());

    private static (string Markup, string Json) SplitStateIsland(string html)
    {
        const string start = "<script type=\"application/json\" data-viu-state>";
        const string end = "</script>";
        int startIndex = html.IndexOf(start, StringComparison.Ordinal);
        startIndex.ShouldBeGreaterThanOrEqualTo(0);
        int jsonStart = startIndex + start.Length;
        int endIndex = html.IndexOf(end, jsonStart, StringComparison.Ordinal);
        endIndex.ShouldBeGreaterThanOrEqualTo(jsonStart);
        (endIndex + end.Length).ShouldBe(html.Length);
        return (
            html[..startIndex],
            html.Substring(jsonStart, endIndex - jsonStart));
    }

    private sealed class SsrStateComponent : IComponent
    {
        private readonly ConcurrentRenderBarrier? _barrier;
        private readonly StateStoreDefinition<SsrPayloadStore> _definition;

        internal SsrStateComponent(
            StateStoreDefinition<SsrPayloadStore> definition,
            ConcurrentRenderBarrier? barrier)
        {
            _definition = definition;
            _barrier = barrier;
        }

        public ComponentRenderer Setup(ComponentContext context)
        {
            SsrPayloadStore stateStore = _definition.Use(context);
            if (_barrier is not null)
            {
                context.Lifecycle.OnServerPrefetch(_barrier.WaitAsync);
            }

            return _ => new ElementNode(
                new QualifiedName("p"),
                children: [new TextNode(stateStore.State.Message)]);
        }
    }

    private sealed class StringServiceProvider : IServiceProvider
    {
        private readonly string _value;

        internal StringServiceProvider(string value)
        {
            _value = value;
        }

        public object? GetService(Type serviceType) =>
            serviceType == typeof(string) ? _value : null;
    }

    private sealed class ConcurrentRenderBarrier
    {
        private readonly TaskCompletionSource _bothEntered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _entered;

        internal async Task WaitAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _entered) == 2)
            {
                _bothEntered.TrySetResult();
            }

            await _bothEntered.Task.WaitAsync(cancellationToken);
        }
    }
}

internal sealed class SsrPayloadStore
{
    internal SsrPayloadState State { get; set; } = new();
}

internal sealed class SsrPayloadState
{
    public string Message { get; set; } = string.Empty;
}

[JsonSerializable(typeof(SsrPayloadState))]
internal sealed partial class ServerRendererStateJsonContext : JsonSerializerContext
{
}
