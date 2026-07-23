using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.Tests;

public sealed class ViuWatchTests : IDisposable
{
    private readonly TestSchedulerPump _pump;

    public ViuWatchTests()
    {
        Scheduler.Reset();
        _pump = TestSchedulerPump.Install();
    }

    public void Dispose()
    {
        Scheduler.FlushBoundaryCallback = null;
        Scheduler.Reset();
        _pump.Dispose();
    }

    [Fact]
    public void Watch_ReferenceDefaultsToPreFlushAndStopsWithComponentScope()
    {
        IReactiveReference<int> count = Reactive.Reference(0);
        List<(int Current, int Previous)> values = [];
        MountedComponent mounted = CreateMounted(
            () => ViuWatch.Watch(
                count,
                (current, previous, _) => values.Add((current, previous))));

        count.Value = 1;
        count.Value = 2;

        values.ShouldBeEmpty();
        _pump.PendingFlushCount.ShouldBe(1);
        _pump.RunUntilIdle();
        values.ShouldBe([(2, 0)]);

        mounted.Unmount(static () => { });
        count.Value = 3;
        _pump.RunUntilIdle();
        values.ShouldBe([(2, 0)]);
    }

    [Fact]
    public void Watch_ExplicitSyncOptionRunsInline()
    {
        IReactiveReference<int> count = Reactive.Reference(0);
        List<int> values = [];
        MountedComponent mounted = CreateMounted(
            () => ViuWatch.Watch(
                count,
                (current, _, _) => values.Add(current),
                new WatchOptions { Flush = WatchFlushMode.Sync }));

        count.Value = 1;

        values.ShouldBe([1]);
        _pump.PendingFlushCount.ShouldBe(0);
        mounted.Unmount(static () => { });
    }

    [Fact]
    public void Watch_ExplicitPostOptionRunsAfterRenderQueue()
    {
        IReactiveReference<int> count = Reactive.Reference(0);
        List<string> order = [];
        MountedComponent mounted = CreateMounted(
            () => ViuWatch.Watch(
                count,
                (_, _, _) => order.Add("watch"),
                new WatchOptions { Flush = WatchFlushMode.Post }));

        count.Value = 1;
        Scheduler.QueueJob(
            new SchedulerJob(() => order.Add("render"))
            {
                Identifier = 1,
            });
        _pump.RunUntilIdle();

        order.ShouldBe(["render", "watch"]);
        mounted.Unmount(static () => { });
    }

    [Fact]
    public void ApplicationWatchScheduler_ReusedAcrossWatchersMaintainsOneJobPerWatcher()
    {
        ApplicationWatchScheduler scheduler = new();
        IReactiveReference<int> first = Reactive.Reference(0);
        IReactiveReference<int> second = Reactive.Reference(0);
        int firstRuns = 0;
        int secondRuns = 0;
        using WatchHandle firstWatch = Reactive.Watch(
            first,
            (_, _, _) => firstRuns++,
            new WatchOptions
            {
                Flush = WatchFlushMode.Pre,
                Scheduler = scheduler,
            });
        using WatchHandle secondWatch = Reactive.Watch(
            second,
            (_, _, _) => secondRuns++,
            new WatchOptions
            {
                Flush = WatchFlushMode.Pre,
                Scheduler = scheduler,
            });

        first.Value = 1;
        first.Value = 2;
        second.Value = 1;
        _pump.RunUntilIdle();

        firstRuns.ShouldBe(1);
        secondRuns.ShouldBe(1);
    }

    [Fact]
    public void ApplicationWatchScheduler_ComponentIdentifierOrdersPreWatchBeforeOwnRender()
    {
        ApplicationWatchScheduler scheduler = new(componentIdentifier: 4);
        IReactiveReference<int> count = Reactive.Reference(0);
        List<string> order = [];
        using WatchHandle watch = Reactive.Watch(
            count,
            (_, _, _) => order.Add("watch"),
            new WatchOptions
            {
                Flush = WatchFlushMode.Pre,
                Scheduler = scheduler,
            });

        Scheduler.QueueJob(
            new SchedulerJob(() => order.Add("child-render"))
            {
                Identifier = 5,
            });
        Scheduler.QueueJob(
            new SchedulerJob(() => order.Add("own-render"))
            {
                Identifier = 4,
            });
        Scheduler.QueueJob(
            new SchedulerJob(() => order.Add("parent-render"))
            {
                Identifier = 3,
            });
        count.Value = 1;
        _pump.RunUntilIdle();

        order.ShouldBe(["parent-render", "watch", "own-render", "child-render"]);
    }

    [Fact]
    public void Watch_MultipleReferencesAndGettersPreserveAlignedValues()
    {
        IReactiveReference<int> first = Reactive.Reference(1);
        IReactiveReference<int> second = Reactive.Reference(2);
        object?[]? referenceValues = null;
        object?[]? getterValues = null;
        MountedComponent mounted = CreateMounted(
            () =>
            {
                ViuWatch.Watch(
                    new IReactiveReference[] { first, second },
                    (current, _, _) => referenceValues = current);
                ViuWatch.Watch(
                    new Func<object?>[]
                    {
                        () => first.Value,
                        () => second.Value,
                    },
                    (current, _, _) => getterValues = current);
            });

        first.Value = 3;
        _pump.RunUntilIdle();

        referenceValues.ShouldBe([3, 2]);
        getterValues.ShouldBe([3, 2]);
        mounted.Unmount(static () => { });
    }

    [Fact]
    public void Watch_ReactiveObjectIsDeepByDefault()
    {
        TestReactiveObject source = new();
        int runs = 0;
        MountedComponent mounted = CreateMounted(
            () => ViuWatch.Watch(source, (_, _, _) => runs++));

        source.Value = 1;
        _pump.RunUntilIdle();

        runs.ShouldBe(1);
        mounted.Unmount(static () => { });
    }

    [Fact]
    public void WatchEffect_RunsImmediatelyThenUsesPreFlushScheduling()
    {
        IReactiveReference<int> count = Reactive.Reference(0);
        int runs = 0;
        MountedComponent mounted = CreateMounted(
            () => ViuWatch.WatchEffect(
                () =>
                {
                    _ = count.Value;
                    runs++;
                }));

        runs.ShouldBe(1);
        count.Value = 1;
        count.Value = 2;
        runs.ShouldBe(1);
        _pump.RunUntilIdle();

        runs.ShouldBe(2);
        mounted.Unmount(static () => { });
    }

    [Fact]
    public void GetterCallbackAndEffectErrorsRouteThroughApplicationHandler()
    {
        IReactiveReference<int> count = Reactive.Reference(0);
        List<(string Message, string DiagnosticInformation)> errors = [];
        MountedComponent mounted = CreateMounted(
            () =>
            {
                ViuWatch.Watch<int>(
                    () => throw new InvalidOperationException("getter failed"),
                    static (_, _, _) => { });
                ViuWatch.WatchEffect(
                    () => throw new InvalidOperationException("effect failed"));
                ViuWatch.Watch(
                    count,
                    static (_, _, _) =>
                        throw new InvalidOperationException("callback failed"));
            },
            application =>
                application.ErrorHandler = (exception, _, diagnosticInformation) =>
                    errors.Add((exception.Message, diagnosticInformation)));

        errors.ShouldBe(
        [
            ("getter failed", "watcher getter"),
            ("effect failed", "watcher callback"),
        ]);

        count.Value = 1;
        _pump.RunUntilIdle();

        errors.ShouldBe(
        [
            ("getter failed", "watcher getter"),
            ("effect failed", "watcher callback"),
            ("callback failed", "watcher callback"),
        ]);
        mounted.Unmount(static () => { });
    }

    private static MountedComponent CreateMounted(
        Action setup,
        Action<IApplicationContext>? configure = null)
    {
        DelegateTemplate template = new(setup);
        ComponentFactory factory = new(
        [
            new ComponentRegistration(
                typeof(DelegateTemplate),
                () => template),
        ]);
        ITemplateComponent request = ComponentTree.Template<DelegateTemplate>();
        IApplicationContext application = new ApplicationContext(
            request,
            factory,
            new EmptyServiceProvider());
        configure?.Invoke(application);
        return MountedComponent.Create(application, request);
    }

    private sealed class DelegateTemplate : IComponentTemplate
    {
        private readonly Action _setup;

        internal DelegateTemplate(Action setup)
        {
            _setup = setup;
        }

        public ComponentRenderer Setup(IComponentContext context)
        {
            _setup();
            return () => ComponentTree.Comment();
        }
    }

    private sealed class TestReactiveObject : IReactiveObject
    {
        private readonly Dependency _dependency = new();
        private int _value;

        internal int Value
        {
            get
            {
                _dependency.Track();
                return _value;
            }
            set
            {
                if (_value == value)
                {
                    return;
                }

                _value = value;
                _dependency.Trigger();
            }
        }

        public object ToRaw()
        {
            return this;
        }

        public Dependency? GetDependency(string propertyName)
        {
            return propertyName == nameof(Value) ? _dependency : null;
        }

        public void Traverse(ReactiveTraversal traversal)
        {
            _ = Value;
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return null;
        }
    }
}
