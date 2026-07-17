using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Vue.Testing;

namespace Assimalign.Vue.RuntimeCore.Tests;

// Pins provide/inject against @vue/runtime-core's apiInject.ts —
// https://vuejs.org/guide/components/provide-inject.html. Exercised through the in-memory renderer
// so provide/inject run in real Setup windows with the current-instance stack live.
public class DependencyInjectionTests : IDisposable
{
    private readonly TestRenderer _renderer = new();
    private readonly TestElement _container;
    private readonly TestSchedulerPump _pump;

    public DependencyInjectionTests()
    {
        Scheduler.Reset();
        _pump = TestSchedulerPump.Install();
        _container = _renderer.CreateContainer();
    }

    public void Dispose()
    {
        Scheduler.Reset();
        _pump.Dispose();
    }

    [Fact]
    public void Inject_ResolvesAncestorProvidedValue_AtAnyDepth()
    {
        var key = new InjectionKey<string>("message");
        string? injected = null;

        var grandchild = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                injected = DependencyInjection.Inject(key);
                return static () => VirtualNodeFactory.Text("leaf");
            },
        };
        var child = new TestComponent
        {
            SetupFunction = (_, _) => () => VirtualNodeFactory.Component(grandchild),
        };
        var parent = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                DependencyInjection.Provide(key, "hello");
                return () => VirtualNodeFactory.Component(child);
            },
        };

        _renderer.Render(VirtualNodeFactory.Component(parent), _container);

        injected.ShouldBe("hello");
    }

    [Fact]
    public void NearerProvider_ShadowsFartherProvider()
    {
        var key = new InjectionKey<string>("k");
        string? injected = null;

        var leaf = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                injected = DependencyInjection.Inject(key);
                return static () => VirtualNodeFactory.Text("x");
            },
        };
        var middle = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                DependencyInjection.Provide(key, "near");
                return () => VirtualNodeFactory.Component(leaf);
            },
        };
        var root = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                DependencyInjection.Provide(key, "far");
                return () => VirtualNodeFactory.Component(middle);
            },
        };

        _renderer.Render(VirtualNodeFactory.Component(root), _container);

        injected.ShouldBe("near");
    }

    [Fact]
    public void Inject_ResolvesThroughADeepTree()
    {
        // Deep-tree injection: every level probes the same provided value in O(1) via the layered
        // provides table (no parent-chain walk on the read path).
        var key = new InjectionKey<int>("level");
        var seen = new List<int?>();

        IComponentDefinition Build(int depth)
        {
            if (depth == 0)
            {
                return new TestComponent
                {
                    SetupFunction = (_, _) =>
                    {
                        seen.Add(DependencyInjection.Inject(key));
                        return static () => VirtualNodeFactory.Text("leaf");
                    },
                };
            }
            var inner = Build(depth - 1);
            return new TestComponent
            {
                SetupFunction = (_, _) =>
                {
                    seen.Add(DependencyInjection.Inject(key));
                    return () => VirtualNodeFactory.Component(inner);
                },
            };
        }

        var chain = Build(10);
        var root = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                DependencyInjection.Provide(key, 7);
                return () => VirtualNodeFactory.Component(chain);
            },
        };

        _renderer.Render(VirtualNodeFactory.Component(root), _container);

        seen.Count.ShouldBe(11);
        seen.ShouldAllBe(value => value == 7);
    }

    [Fact]
    public void Inject_MissingKey_UsesDefaultValueWithoutWarning()
    {
        using var warnings = new WarningCapture();
        var key = new InjectionKey<int>("count");
        var injected = -1;

        var component = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                injected = DependencyInjection.Inject(key, 42);
                return static () => VirtualNodeFactory.Text("x");
            },
        };

        _renderer.Render(VirtualNodeFactory.Component(component), _container);

        injected.ShouldBe(42);
        warnings.Messages.ShouldBeEmpty();
    }

    [Fact]
    public void Inject_DefaultFactory_RunsOnlyOnMiss()
    {
        var key = new InjectionKey<string>("k");
        var factoryRuns = 0;
        string? missResult = null;
        string? hitResult = null;

        var missComponent = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                missResult = DependencyInjection.Inject(key, () =>
                {
                    factoryRuns++;
                    return "made";
                });
                return static () => VirtualNodeFactory.Text("x");
            },
        };
        _renderer.Render(VirtualNodeFactory.Component(missComponent), _container);
        missResult.ShouldBe("made");
        factoryRuns.ShouldBe(1);

        var hitLeaf = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                hitResult = DependencyInjection.Inject(key, () =>
                {
                    factoryRuns++;
                    return "made";
                });
                return static () => VirtualNodeFactory.Text("y");
            },
        };
        var provider = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                DependencyInjection.Provide(key, "provided");
                return () => VirtualNodeFactory.Component(hitLeaf);
            },
        };
        _renderer.Render(VirtualNodeFactory.Component(provider), _renderer.CreateContainer());

        hitResult.ShouldBe("provided");
        factoryRuns.ShouldBe(1); // never invoked on a hit
    }

    [Fact]
    public void Inject_MissingKeyWithNoDefault_WarnsAndReturnsDefault()
    {
        using var warnings = new WarningCapture();
        var key = new InjectionKey<string>("absent");
        var injected = "sentinel";

        var component = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                injected = DependencyInjection.Inject(key)!;
                return static () => VirtualNodeFactory.Text("x");
            },
        };

        _renderer.Render(VirtualNodeFactory.Component(component), _container);

        injected.ShouldBeNull();
        warnings.Messages.ShouldContain(message => message.Contains("not found"));
    }

    [Fact]
    public void ComponentThatProvidesAndInjectsSameKey_SeesAncestorValue()
    {
        // Upstream reads from the parent's provides, so a self-provide is invisible to the same
        // component's own inject — it resolves the ancestor value instead.
        var key = new InjectionKey<string>("k");
        string? injected = null;

        var child = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                DependencyInjection.Provide(key, "childs-own");
                injected = DependencyInjection.Inject(key, "fallback");
                return static () => VirtualNodeFactory.Text("x");
            },
        };
        var root = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                DependencyInjection.Provide(key, "ancestor");
                return () => VirtualNodeFactory.Component(child);
            },
        };

        _renderer.Render(VirtualNodeFactory.Component(root), _container);

        injected.ShouldBe("ancestor");
    }

    [Fact]
    public void ProvideInOneChild_IsNotVisibleToSibling()
    {
        var key = new InjectionKey<string>("k");
        var siblingInjected = "sentinel";

        var provider = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                DependencyInjection.Provide(key, "secret");
                return static () => VirtualNodeFactory.Text("p");
            },
        };
        var sibling = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                siblingInjected = DependencyInjection.Inject(key, "none");
                return static () => VirtualNodeFactory.Text("s");
            },
        };
        var root = new TestComponent
        {
            SetupFunction = (_, _) => () => VirtualNodeFactory.Element(
                "div",
                VirtualNodeFactory.Component(provider),
                VirtualNodeFactory.Component(sibling)),
        };

        _renderer.Render(VirtualNodeFactory.Component(root), _container);

        siblingInjected.ShouldBe("none");
    }

    [Fact]
    public void StringKeys_AreSupported()
    {
        object? injected = null;

        var child = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                injected = DependencyInjection.Inject("theme");
                return static () => VirtualNodeFactory.Text("x");
            },
        };
        var root = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                DependencyInjection.Provide("theme", "dark");
                return () => VirtualNodeFactory.Component(child);
            },
        };

        _renderer.Render(VirtualNodeFactory.Component(root), _container);

        injected.ShouldBe("dark");
    }

    [Fact]
    public void StringInject_TreatDefaultAsFactory_InvokesDelegateOnlyWhenAsked()
    {
        object? asValue = null;
        object? asFactory = null;
        Func<object?> factory = () => "computed";

        var component = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                // Without the flag the delegate is returned verbatim (upstream: default is a value).
                asValue = DependencyInjection.Inject("absent", factory);
                // With the flag it is invoked (upstream: treatDefaultAsFactory).
                asFactory = DependencyInjection.Inject("absent", factory, treatDefaultAsFactory: true);
                return static () => VirtualNodeFactory.Text("x");
            },
        };

        _renderer.Render(VirtualNodeFactory.Component(component), _container);

        asValue.ShouldBeSameAs(factory);
        asFactory.ShouldBe("computed");
    }

    [Fact]
    public void InjectionKeys_AreIdentityBased_NotNameBased()
    {
        // Parity with upstream's Symbol keys: identity, not description, distinguishes keys.
        var keyA = new InjectionKey<string>("same-name");
        var keyB = new InjectionKey<string>("same-name");
        string? viaA = null;
        var viaB = "sentinel";

        var child = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                viaA = DependencyInjection.Inject(keyA);
                viaB = DependencyInjection.Inject(keyB, "fallback-b");
                return static () => VirtualNodeFactory.Text("x");
            },
        };
        var root = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                DependencyInjection.Provide(keyA, "onlyA");
                return () => VirtualNodeFactory.Component(child);
            },
        };

        _renderer.Render(VirtualNodeFactory.Component(root), _container);

        viaA.ShouldBe("onlyA");
        viaB.ShouldBe("fallback-b");
    }

    [Fact]
    public void ProvideAndInject_OutsideSetup_Warn()
    {
        using var warnings = new WarningCapture();
        ComponentInstance.Current.ShouldBeNull();

        DependencyInjection.Provide("k", "v");
        var result = DependencyInjection.Inject("k", "fallback");

        result.ShouldBe("fallback");
        warnings.Messages.ShouldContain(message => message.Contains("provide()"));
        warnings.Messages.ShouldContain(message => message.Contains("inject()"));
    }
}
