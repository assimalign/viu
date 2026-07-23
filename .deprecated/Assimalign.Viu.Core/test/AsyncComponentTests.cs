using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Tests;

// Pins DefineAsyncComponent against the in-memory renderer, DOM-free — mirroring upstream
// runtime-core/__tests__/apiAsyncComponent.spec.ts. A loader returns the real component
// asynchronously; a loading component shows after Delay, an error component on failure or Timeout,
// and the resolved component renders in place, cached so later mounts reuse it without reloading.
// Contract: packages/runtime-core/src/apiAsyncComponent.ts and
// https://vuejs.org/guide/components/async.html. Timers flow through the AsyncComponentDelay seam so
// a ManualTimeController drives them deterministically (no wall-clock waits). [V01.01.03.16]
public sealed class AsyncComponentTests : IDisposable
{
    private readonly TestRenderer _renderer = new();
    private readonly TestElement _container;
    private readonly TestSchedulerPump _pump;
    private readonly ManualTimeController _time;
    private readonly SynchronizationContext? _previousContext;
    private int _realSetups;
    private ReactiveValue<int>? _realState;

    public AsyncComponentTests()
    {
        Scheduler.Reset();
        _pump = TestSchedulerPump.Install();
        _time = new ManualTimeController();
        // Production runs on the browser's single-threaded WASM SynchronizationContext, so an awaited
        // loader's continuation always resumes on the main thread. A plain xUnit host has no context,
        // so awaited continuations can hop to the thread pool non-deterministically — install a
        // single-threaded context that runs them inline on the test thread (mirroring WASM), keeping
        // the async flow deterministic and single-threaded.
        _previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new InlineSynchronizationContext());
        _container = _renderer.CreateContainer();
    }

    public void Dispose()
    {
        Scheduler.Reset();
        _time.Dispose();
        _pump.Dispose();
        // Defensively clear the ambient boundary so a suspensible test can never leak it to the next.
        SuspenseBoundaryContext.Current = null;
        SynchronizationContext.SetSynchronizationContext(_previousContext);
    }

    private string Serialize() => TestNodeSerializer.Serialize(_container);

    // A stateful resolved component: counts its Setup runs and exposes its reactive state ref so a
    // KeepAlive test can prove the state survives a switch cycle. Renders "<div>{label}{state}</div>".
    private TestComponent RealComponent(string label = "real") => new()
    {
        Name = "Real",
        SetupFunction = (_, _) =>
        {
            _realSetups++;
            var state = Reactive.Reference(0);
            _realState = state;
            return () => VirtualNodeFactory.Element("div", $"{label}{state.Value}");
        },
    };

    private static TestComponent LoadingComponent() => new()
    {
        Name = "Loading",
        SetupFunction = (_, _) => () => VirtualNodeFactory.Element("div", "loading"),
    };

    // Shows the error prop it receives (upstream: errorComponent is passed { error }).
    private static TestComponent ErrorDisplayComponent() => new()
    {
        Name = "ErrorDisplay",
        Properties = [new ComponentPropertyDefinition("error")],
        SetupFunction = (properties, _) => () =>
            VirtualNodeFactory.Element("div", $"error:{(properties["error"] as Exception)?.Message}"),
    };

    // --- loader invocation / caching -------------------------------------------------------------

    [Fact]
    public void DefineAsyncComponent_DoesNotInvokeLoaderUntilMounted()
    {
        var loaderCalls = 0;
        _ = AsyncComponents.DefineAsyncComponent(() =>
        {
            loaderCalls++;
            return Task.FromResult<IComponent>(RealComponent());
        });

        // Defining the component must not run the loader — it runs on first mount (upstream parity).
        loaderCalls.ShouldBe(0);
    }

    [Fact]
    public void Loader_RunsOnceOnFirstMount_AndResolutionReRendersThroughScheduler()
    {
        var loaderCalls = 0;
        var request = new TaskCompletionSource<IComponent>();
        var real = RealComponent();
        var async = AsyncComponents.DefineAsyncComponent(() =>
        {
            loaderCalls++;
            return request.Task;
        });

        _renderer.Render(VirtualNodeFactory.Component(async), _container);

        // First mount: loader invoked exactly once; the pending load renders a comment placeholder
        // (default 200ms delay not elapsed, no loading component).
        loaderCalls.ShouldBe(1);
        Serialize().ShouldBe("<root><!----></root>");
        _pump.PendingFlushCount.ShouldBe(0);

        // Resolving the load flips a ref that schedules a flush (no polling) — pin the scheduled flush.
        request.SetResult(real);
        _pump.PendingFlushCount.ShouldBeGreaterThan(0);
        _pump.RunUntilIdle();

        Serialize().ShouldBe("<root><div>real0</div></root>");
        loaderCalls.ShouldBe(1);
    }

    [Fact]
    public void Loader_ConcurrentMountsShareOneInflightLoad_InvokedOnce()
    {
        var loaderCalls = 0;
        var request = new TaskCompletionSource<IComponent>();
        var real = RealComponent();
        var async = AsyncComponents.DefineAsyncComponent(() =>
        {
            loaderCalls++;
            return request.Task;
        });

        // Two mounts of the SAME async component in one turn share one in-flight load.
        _renderer.Render(
            VirtualNodeFactory.Fragment(VirtualNodeFactory.Component(async), VirtualNodeFactory.Component(async)),
            _container);
        loaderCalls.ShouldBe(1);

        request.SetResult(real);
        _pump.RunUntilIdle();

        Serialize().ShouldBe("<root><div>real0</div><div>real0</div></root>");
        loaderCalls.ShouldBe(1);
    }

    [Fact]
    public void ResolvedComponent_CachedAcrossRemounts_LoaderNotReinvoked()
    {
        var loaderCalls = 0;
        var real = RealComponent();
        var async = AsyncComponents.DefineAsyncComponent(() =>
        {
            loaderCalls++;
            return Task.FromResult<IComponent>(real);
        });

        _renderer.Render(VirtualNodeFactory.Component(async), _container);
        _pump.RunUntilIdle();
        Serialize().ShouldBe("<root><div>real0</div></root>");
        loaderCalls.ShouldBe(1);

        // Unmount, then remount: the cached resolved definition is reused without a second load.
        _renderer.Render(null, _container);
        _renderer.Render(VirtualNodeFactory.Component(async), _container);
        _pump.RunUntilIdle();

        Serialize().ShouldBe("<root><div>real0</div></root>");
        loaderCalls.ShouldBe(1);
    }

    // --- loading / delay -------------------------------------------------------------------------

    [Fact]
    public void LoadingComponent_AppearsOnlyAfterDelay_ThenResolvedReplacesIt()
    {
        var loaderCalls = 0;
        var request = new TaskCompletionSource<IComponent>();
        var real = RealComponent();
        var async = AsyncComponents.DefineAsyncComponent(new AsyncComponentOptions
        {
            Loader = () =>
            {
                loaderCalls++;
                return request.Task;
            },
            LoadingComponent = LoadingComponent(),
            Delay = 200,
        });

        _renderer.Render(VirtualNodeFactory.Component(async), _container);

        // Before the delay elapses, the loading component is hidden (upstream: delayed = ref(!!delay)).
        Serialize().ShouldBe("<root><!----></root>");

        _time.Advance(199);
        _pump.RunUntilIdle();
        Serialize().ShouldBe("<root><!----></root>");

        // The delay elapses: the loading component appears.
        _time.Advance(1);
        _pump.RunUntilIdle();
        Serialize().ShouldBe("<root><div>loading</div></root>");

        // The load resolves: the resolved component replaces the loading one; loader ran once.
        request.SetResult(real);
        _pump.RunUntilIdle();
        Serialize().ShouldBe("<root><div>real0</div></root>");
        loaderCalls.ShouldBe(1);
    }

    [Fact]
    public void LoadingComponent_WithZeroDelay_AppearsImmediately()
    {
        var request = new TaskCompletionSource<IComponent>();
        var async = AsyncComponents.DefineAsyncComponent(new AsyncComponentOptions
        {
            Loader = () => request.Task,
            LoadingComponent = LoadingComponent(),
            Delay = 0,
        });

        _renderer.Render(VirtualNodeFactory.Component(async), _container);

        // delay 0 shows the loading component on the first render (upstream: delayed = ref(false)).
        Serialize().ShouldBe("<root><div>loading</div></root>");
    }

    // --- error / timeout -------------------------------------------------------------------------

    [Fact]
    public void ErrorComponent_RendersOnLoaderFailure_WithErrorProp()
    {
        var request = new TaskCompletionSource<IComponent>();
        var async = AsyncComponents.DefineAsyncComponent(new AsyncComponentOptions
        {
            Loader = () => request.Task,
            ErrorComponent = ErrorDisplayComponent(),
        });

        _renderer.Render(VirtualNodeFactory.Component(async), _container);

        request.SetException(new InvalidOperationException("boom"));
        _pump.RunUntilIdle();

        // The error component renders, receiving the failure as its "error" prop.
        Serialize().ShouldBe("<root><div>error:boom</div></root>");
    }

    [Fact]
    public void ErrorComponent_RendersAfterTimeoutElapses()
    {
        var request = new TaskCompletionSource<IComponent>(); // never resolves
        var async = AsyncComponents.DefineAsyncComponent(new AsyncComponentOptions
        {
            Loader = () => request.Task,
            ErrorComponent = ErrorDisplayComponent(),
            Timeout = 100,
        });

        _renderer.Render(VirtualNodeFactory.Component(async), _container);
        Serialize().ShouldBe("<root><!----></root>");

        // The timeout elapses without a resolution: the error component renders a timeout error.
        _time.Advance(100);
        _pump.RunUntilIdle();
        Serialize().ShouldBe("<root><div>error:Async component timed out after 100ms.</div></root>");
    }

    [Fact]
    public void LoaderFailure_WithNoErrorComponent_RoutesToAppErrorHandler_WithoutCrashing()
    {
        Exception? handled = null;
        var request = new TaskCompletionSource<IComponent>();
        var async = AsyncComponents.DefineAsyncComponent(() => request.Task);
        var application = _renderer.Renderer.CreateApplication(async);
        application.Context.ErrorHandler = (exception, _, _) => handled = exception;
        application.Mount(_container);

        // A load failure with no error component routes through the app error handler (upstream:
        // handleError) instead of aborting the flush.
        request.SetException(new InvalidOperationException("unhandled"));
        _pump.RunUntilIdle();

        handled.ShouldBeOfType<InvalidOperationException>();
        handled!.Message.ShouldBe("unhandled");
    }

    // --- onError retry / fail --------------------------------------------------------------------

    [Fact]
    public void OnError_RetryReinvokesLoader_WithIncrementingAttempts_UntilSuccess()
    {
        var loaderCalls = 0;
        var attempts = new List<int>();
        var real = RealComponent();
        var async = AsyncComponents.DefineAsyncComponent(new AsyncComponentOptions
        {
            Loader = () =>
            {
                loaderCalls++;
                // Fail the first two attempts, succeed on the third.
                return loaderCalls < 3
                    ? Task.FromException<IComponent>(new InvalidOperationException($"fail{loaderCalls}"))
                    : Task.FromResult<IComponent>(real);
            },
            OnError = (_, retry, fail, attempt) =>
            {
                attempts.Add(attempt);
                if (attempt <= 2)
                {
                    retry();
                }
                else
                {
                    fail();
                }
            },
        });

        _renderer.Render(VirtualNodeFactory.Component(async), _container);
        _pump.RunUntilIdle();

        // Retry re-runs the loader with an incrementing one-based attempt count; the third succeeds.
        Serialize().ShouldBe("<root><div>real0</div></root>");
        loaderCalls.ShouldBe(3);
        attempts.ShouldBe([1, 2]);
    }

    [Fact]
    public void OnError_FailSettlesToErrorState_LoaderInvokedOnce()
    {
        var loaderCalls = 0;
        var async = AsyncComponents.DefineAsyncComponent(new AsyncComponentOptions
        {
            Loader = () =>
            {
                loaderCalls++;
                return Task.FromException<IComponent>(new InvalidOperationException("nope"));
            },
            ErrorComponent = ErrorDisplayComponent(),
            OnError = (_, _, fail, _) => fail(),
        });

        _renderer.Render(VirtualNodeFactory.Component(async), _container);
        _pump.RunUntilIdle();

        // fail() settles to the error state without retrying: loader invoked exactly once.
        Serialize().ShouldBe("<root><div>error:nope</div></root>");
        loaderCalls.ShouldBe(1);
    }

    // --- teardown --------------------------------------------------------------------------------

    [Fact]
    public void UnmountBeforeResolution_DiscardsPendingRender_NoErrorsOrLeaks()
    {
        var request = new TaskCompletionSource<IComponent>();
        var real = RealComponent();
        var async = AsyncComponents.DefineAsyncComponent(new AsyncComponentOptions
        {
            Loader = () => request.Task,
            LoadingComponent = LoadingComponent(),
            Delay = 0,
            Timeout = 100,
        });

        _renderer.Render(VirtualNodeFactory.Component(async), _container);
        Serialize().ShouldBe("<root><div>loading</div></root>");

        // Unmount before the load resolves.
        _renderer.Render(null, _container);
        Serialize().ShouldBe("<root></root>");

        // A late resolution and a late timeout timer must neither re-render nor throw (the pending
        // render is discarded, timers disposed): the resolved component never mounts.
        request.SetResult(real);
        _time.Advance(100);
        _pump.RunUntilIdle();

        Serialize().ShouldBe("<root></root>");
        _realSetups.ShouldBe(0);
    }

    // --- suspensible seam ([V01.01.03.20] completes the boundary) --------------------------------

    [Fact]
    public void Suspensible_WithBoundary_RegistersPendingLoad_AndDefersDisplayToBoundary()
    {
        var request = new TaskCompletionSource<IComponent>();
        var real = RealComponent();
        var boundary = new FakeSuspenseBoundary();
        var async = AsyncComponents.DefineAsyncComponent(new AsyncComponentOptions
        {
            Loader = () => request.Task,
            LoadingComponent = LoadingComponent(),
            Delay = 0,
            Suspensible = true,
        });

        // A boundary is present while the async component mounts (the seam a future Suspense sets).
        SuspenseBoundaryContext.Current = boundary;
        try
        {
            _renderer.Render(VirtualNodeFactory.Component(async), _container);
        }
        finally
        {
            SuspenseBoundaryContext.Current = null;
        }

        // The boundary received the in-flight load; the component shows none of its own loading UI
        // (the boundary owns the fallback) — a comment placeholder, not the loading component.
        boundary.Registrations.Count.ShouldBe(1);
        boundary.Registrations[0].Instance.ShouldNotBeNull();
        boundary.Registrations[0].PendingLoad.ShouldNotBeNull();
        Serialize().ShouldBe("<root><!----></root>");

        // Once resolved, the boundary-controlled component still renders its resolved subtree.
        request.SetResult(real);
        _pump.RunUntilIdle();
        Serialize().ShouldBe("<root><div>real0</div></root>");
    }

    [Fact]
    public void NonSuspensible_WithBoundary_IgnoresBoundary_ShowsOwnLoadingUI()
    {
        var request = new TaskCompletionSource<IComponent>();
        var boundary = new FakeSuspenseBoundary();
        var async = AsyncComponents.DefineAsyncComponent(new AsyncComponentOptions
        {
            Loader = () => request.Task,
            LoadingComponent = LoadingComponent(),
            Delay = 0,
            Suspensible = false,
        });

        SuspenseBoundaryContext.Current = boundary;
        try
        {
            _renderer.Render(VirtualNodeFactory.Component(async), _container);
        }
        finally
        {
            SuspenseBoundaryContext.Current = null;
        }

        // suspensible = false ignores the boundary and renders its own loading component.
        boundary.Registrations.ShouldBeEmpty();
        Serialize().ShouldBe("<root><div>loading</div></root>");
    }

    // --- KeepAlive interplay ([V01.01.03.18]) ----------------------------------------------------

    [Fact]
    public void KeptAliveAsyncComponent_PreservesResolvedStateAcrossSwitches_LoaderRunsOnce()
    {
        var loaderCalls = 0;
        var request = new TaskCompletionSource<IComponent>();
        var real = RealComponent();
        var other = new TestComponent
        {
            Name = "Other",
            SetupFunction = (_, _) => () => VirtualNodeFactory.Element("div", "other"),
        };
        var async = AsyncComponents.DefineAsyncComponent(() =>
        {
            loaderCalls++;
            return request.Task;
        });

        var view = Reactive.Reference("async");
        var slots = new ComponentSlots();
        slots["default"] = _ => [view.Value == "async" ? VirtualNodeFactory.Component(async) : VirtualNodeFactory.Component(other)];
        _renderer.Render(VirtualNodeFactory.Component(KeepAlive.Instance, null, slots), _container);

        // Resolve the async child while kept alive.
        request.SetResult(real);
        _pump.RunUntilIdle();
        Serialize().ShouldBe("<root><div>real0</div></root>");
        loaderCalls.ShouldBe(1);
        _realSetups.ShouldBe(1);

        // Mutate the resolved component's state, then switch away and back.
        _realState!.Value = 7;
        _pump.RunUntilIdle();
        Serialize().ShouldBe("<root><div>real7</div></root>");

        view.Value = "other";
        _pump.RunUntilIdle();
        Serialize().ShouldBe("<root><div>other</div></root>");

        view.Value = "async";
        _pump.RunUntilIdle();

        // Reactivated with state intact: the cached (resolved inner) subtree was moved back, not
        // remounted — Setup ran once and the loader ran once (upstream KeepAlive + async interplay).
        Serialize().ShouldBe("<root><div>real7</div></root>");
        loaderCalls.ShouldBe(1);
        _realSetups.ShouldBe(1);
    }
}

/// <summary>
/// A single-threaded <see cref="SynchronizationContext"/> that runs posted continuations inline on
/// the current thread — the deterministic test stand-in for the browser's WASM context, so an awaited
/// loader's continuation resumes synchronously on the test thread instead of hopping to the thread
/// pool.
/// </summary>
internal sealed class InlineSynchronizationContext : SynchronizationContext
{
    public override void Post(SendOrPostCallback callback, object? state) => callback(state);

    public override void Send(SendOrPostCallback callback, object? state) => callback(state);
}

/// <summary>Records async-load registrations so the suspensible seam can be asserted.</summary>
internal sealed class FakeSuspenseBoundary : ISuspenseBoundary
{
    public List<(ComponentInstance Instance, Task PendingLoad)> Registrations { get; } = [];

    public void RegisterAsyncDependency(ComponentInstance instance, Task pendingLoad)
        => Registrations.Add((instance, pendingLoad));
}

/// <summary>
/// A deterministic fake clock installed into the <c>AsyncComponentDelay</c> seam: timers are recorded
/// and fire only when the test advances virtual time, so "loading appears after the delay" and the
/// timeout path are pinned without wall-clock waits.
/// </summary>
internal sealed class ManualTimeController : IDisposable
{
    private readonly List<Entry> _entries = [];
    private readonly Func<int, Action, IDisposable>? _previous;
    private long _now;

    public ManualTimeController()
    {
        _previous = AsyncComponentDelay.Scheduler;
        AsyncComponentDelay.Scheduler = Schedule;
    }

    public void Advance(int milliseconds)
    {
        _now += milliseconds;
        foreach (var entry in _entries.Where(e => !e.Cancelled && !e.Fired && e.DueAt <= _now).OrderBy(e => e.DueAt).ToList())
        {
            entry.Fired = true;
            entry.Callback();
        }
    }

    public void Dispose() => AsyncComponentDelay.Scheduler = _previous;

    private IDisposable Schedule(int milliseconds, Action callback)
    {
        var entry = new Entry(_now + milliseconds, callback);
        _entries.Add(entry);
        return entry;
    }

    private sealed class Entry(long dueAt, Action callback) : IDisposable
    {
        public long DueAt { get; } = dueAt;
        public Action Callback { get; } = callback;
        public bool Cancelled { get; private set; }
        public bool Fired { get; set; }
        public void Dispose() => Cancelled = true;
    }
}
