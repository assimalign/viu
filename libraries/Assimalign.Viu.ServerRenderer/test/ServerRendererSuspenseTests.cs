using System;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.ServerRenderer.Tests;

/// <summary>Pins the server-side contract of Core's host-neutral Suspense built-in.</summary>
public sealed class ServerRendererSuspenseTests
{
    [Fact]
    public async Task Render_PendingSuspense_AwaitsAndSerializesOnlyDefaultBranch()
    {
        TaskCompletionSource<AsynchronousComponentTarget> load =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.DefineAsynchronousComponent<AsynchronousIdentity>(
                _ => load.Task);
        int fallbackRenderCount = 0;
        ITemplateComponent root = Suspense.CreateComponent(
            _ => definition.CreateComponent(),
            _ =>
            {
                fallbackRenderCount++;
                return ComponentTree.Text("fallback");
            });
        ComponentFactory components = new(
        [
            Suspense.Registration,
            definition.Registration,
            new ComponentRegistration(
                typeof(ResolvedTemplate),
                static () => new ResolvedTemplate()),
        ]);
        ServerApplication application = new(
            root,
            components,
            TestServiceProvider.Empty);

        Task<string> rendering =
            ServerRenderer.RenderToStringAsync(application);

        rendering.IsCompleted.ShouldBeFalse();
        fallbackRenderCount.ShouldBe(0);

        load.SetResult(
            AsynchronousComponentTarget.From<ResolvedTemplate>());
        string html = await rendering;

        html.ShouldBe("resolved");
        fallbackRenderCount.ShouldBe(0);
    }

    private sealed class AsynchronousIdentity
    {
    }

    private sealed class ResolvedTemplate : IComponentTemplate
    {
        public ComponentRenderer Setup(IComponentContext context)
        {
            _ = context;
            return static () => ComponentTree.Text("resolved");
        }
    }
}
