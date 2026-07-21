using System;

using Assimalign.Viu;
using Assimalign.Viu.Testing;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Store.Tests;

// Pins the per-app install path (Pinia's app.use(pinia), packages/pinia/src/createPinia.ts) and the
// two UseStore resolution modes: from a component Setup via the current app context, and from plain
// C# via a registry handle. Exercised through the in-memory test renderer (DOM-free).
public sealed class StoreApplicationIntegrationTests : IDisposable
{
    private readonly TestSchedulerPump _pump = TestSchedulerPump.Install();

    public void Dispose()
    {
        _pump.RunUntilIdle();
        _pump.Dispose();
        Stores.SetActiveRegistry(null);
    }

    [Fact]
    public void InstalledViaAppUse_ResolvesTheSameStore_InComponentSetup_AsFromDI()
    {
        var registry = new StoreRegistry();
        var useCounter = Stores.DefineStore("counter", () => new CounterStore());
        CounterStore? resolvedInSetup = null;

        var root = new SetupComponent
        {
            SetupFunction = () =>
            {
                // Inside Setup with no explicit registry: resolves via the current app context.
                resolvedInSetup = useCounter.UseStore();
                return static () => VirtualNodeFactory.Text("root");
            },
        };

        var renderer = new TestRenderer();
        var application = renderer.Renderer.CreateApplication(root);
        application.Use(registry.AsPlugin<TestNode>());
        application.Mount(renderer.CreateContainer());

        resolvedInSetup.ShouldNotBeNull();
        // The component-context resolution and the DI resolution are the identical instance.
        resolvedInSetup.ShouldBeSameAs(useCounter.UseStore(registry));
        registry.Count.ShouldBe(1);
    }

    [Fact]
    public void Install_SetsTheAmbientActiveRegistry()
    {
        var registry = new StoreRegistry();
        var renderer = new TestRenderer();
        var application = renderer.Renderer.CreateApplication(
            new SetupComponent { SetupFunction = static () => static () => VirtualNodeFactory.Text("x") });

        application.Use(registry.AsPlugin<TestNode>());

        Stores.ActiveRegistry.ShouldBeSameAs(registry);
    }

    [Fact]
    public void TwoApplications_EachWithItsOwnRegistry_ResolveIsolatedStoresInSetup()
    {
        var useCounter = Stores.DefineStore("counter", () => new CounterStore());

        CounterStore MountWith(StoreRegistry registry)
        {
            CounterStore? resolved = null;
            var root = new SetupComponent
            {
                SetupFunction = () =>
                {
                    resolved = useCounter.UseStore();
                    return static () => VirtualNodeFactory.Text("root");
                },
            };
            var renderer = new TestRenderer();
            var application = renderer.Renderer.CreateApplication(root);
            application.Use(registry.AsPlugin<TestNode>());
            application.Mount(renderer.CreateContainer());
            return resolved!;
        }

        var registry1 = new StoreRegistry();
        var registry2 = new StoreRegistry();
        var store1 = MountWith(registry1);
        var store2 = MountWith(registry2);

        store1.ShouldNotBeSameAs(store2);    // per-app instances (server request isolation)
        store1.Increment();
        store1.Count.Value.ShouldBe(1);
        store2.Count.Value.ShouldBe(0);      // isolated per app
    }

    [Fact]
    public void UseStore_WithNoRegistryAvailable_Throws()
    {
        Stores.SetActiveRegistry(null);
        var useOrphan = Stores.DefineStore("orphan", () => new CounterStore());

        Should.Throw<InvalidOperationException>(() => useOrphan.UseStore());
    }
}
