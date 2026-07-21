using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.Router;

/// <summary>
/// The router instance the built-in components consume — the C# stand-in for the object vue-router's
/// <c>createRouter</c> returns (<c>packages/router/src/router.ts</c>,
/// https://router.vuejs.org/api/#createRouter). It owns the reactive <see cref="CurrentRoute"/>
/// (a <c>shallowRef</c> over the resolved location, mirroring upstream's <c>currentRoute</c>),
/// resolves targets and hrefs through the matcher and history, and drives navigations through the
/// asynchronous, guarded, cancellable pipeline with <see cref="Push"/>/<see cref="Replace"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The navigation pipeline</b> runs guards in vue-router's documented order — in-component
/// <c>beforeRouteLeave</c> (deepest child first) → global <see cref="BeforeEach"/> → reused-record
/// <c>beforeRouteUpdate</c> → per-record <see cref="RouteRecord.BeforeEnter"/> → (async component
/// resolution, currently a no-op seam for <c>[V01.01.03.16]</c>) → in-component
/// <see cref="IRouteEnterGuard"/> → global <see cref="BeforeResolve"/> → confirm (history write +
/// <see cref="CurrentRoute"/> update, one trigger) → <see cref="AfterEach"/>. A guard may allow,
/// abort, or redirect; a redirect re-enters the pipeline (with infinite-redirect protection), an
/// abort leaves <see cref="CurrentRoute"/> and history untouched, and a later navigation cancels an
/// in-flight one through a cooperative <see cref="CancellationToken"/>.
/// </para>
/// <para>
/// <b>The initial navigation</b> mirrors upstream's START-location semantics: <see cref="CurrentRoute"/>
/// begins at <see cref="RouteLocation.Start"/> (empty matched chain), not the eagerly resolved initial
/// location. <see cref="ReadyAsync"/> runs the first navigation to the history location through the
/// same full pipeline with <c>from</c> = the START sentinel — so a global <see cref="BeforeEach"/>
/// redirect fires for a direct page load — and its confirm step replaces (never pushes) the current
/// history entry, exactly as upstream forces a replace when <c>from === START_LOCATION_NORMALIZED</c>.
/// The same-location dedup is skipped whenever <c>from</c> has an empty matched chain, so START never
/// short-circuits the initial pass while in-session duplicates still do.
/// </para>
/// <para>
/// <b>Deliberate C# divergences from vue-router</b> (see <c>docs/DESIGN.md</c>): guards return a
/// <see cref="NavigationGuardResult"/> instead of calling <c>next()</c>; <see cref="Push"/> resolves
/// with a <see cref="NavigationFailure"/> for abort/cancel/duplicate and faults only on genuinely
/// unexpected guard exceptions (routed to <see cref="OnError"/>); and an infinite redirect throws a
/// <see cref="NavigationRedirectException"/> in every configuration rather than only warning in dev.
/// </para>
/// <para>Not thread-safe: the router targets the single-threaded JS event loop.</para>
/// </remarks>
public sealed class Router : IDisposable
{
    private const string DefaultLinkActiveClass = "router-link-active";
    private const string DefaultLinkExactActiveClass = "router-link-exact-active";

    // The redirect-depth safety cap (Viu-specific; upstream relies on a dev-only same-location
    // warning). Kept well above any legitimate redirect chain so only a genuine loop trips it.
    private const int MaximumRedirectDepth = 20;

    private readonly IRouteMatcher _matcher;
    private readonly IRouterHistory _history;
    private readonly ShallowReference<RouteLocation> _currentRoute;
    private readonly Action _unlisten;
    private readonly List<NavigationGuard> _beforeGuards = [];
    private readonly List<NavigationGuard> _beforeResolveGuards = [];
    private readonly List<AfterNavigationHook> _afterHooks = [];
    private readonly List<NavigationErrorHandler> _errorHandlers = [];
    private readonly Dictionary<RouteRecord, List<NavigationGuard>> _leaveGuards = [];
    private readonly Dictionary<RouteRecord, List<NavigationGuard>> _updateGuards = [];
    private CancellationTokenSource? _pendingNavigation;
    private Task<NavigationFailure?>? _initialNavigation;
    private bool _disposed;

    /// <summary>Creates a router over an existing matcher and history.</summary>
    /// <param name="history">The history integration (memory, web, or hash) driving locations.</param>
    /// <param name="matcher">The route table and matcher resolving locations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="history"/> or <paramref name="matcher"/> is null.</exception>
    public Router(IRouterHistory history, IRouteMatcher matcher)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(matcher);
        _history = history;
        _matcher = matcher;
        // Seed the current route with the START sentinel (empty matched chain), not the eagerly
        // resolved initial location, so the first navigation runs the full guard pipeline instead of
        // being deduplicated against a pre-resolved route (upstream: currentRoute starts at
        // START_LOCATION_NORMALIZED; the initial navigation is a real push from START — [V01.01.08.07]).
        _currentRoute = Reactive.ShallowReference(RouteLocation.Start);
        _unlisten = history.Listen(OnHistoryNavigation);
    }

    /// <summary>Creates a router over a fresh <see cref="RouteMatcher"/> built from <paramref name="routes"/>.</summary>
    /// <param name="history">The history integration driving locations.</param>
    /// <param name="routes">The top-level route records.</param>
    /// <exception cref="ArgumentNullException"><paramref name="history"/> or <paramref name="routes"/> is null.</exception>
    public Router(IRouterHistory history, IEnumerable<RouteRecord> routes)
        : this(history, new RouteMatcher(routes))
    {
    }

    /// <summary>
    /// The reactive current location (upstream: <c>router.currentRoute</c>, a <c>shallowRef</c>): one
    /// trigger per completed navigation drives every <see cref="RouterView"/> and the active-class
    /// computation of every <see cref="RouterLink"/> through the normal reactivity graph.
    /// </summary>
    public IReference<RouteLocation> CurrentRoute => _currentRoute;

    /// <summary>
    /// The default active class applied to a <see cref="RouterLink"/> whose target is an inclusive
    /// match of the current route (upstream: the <c>linkActiveClass</c> router option). A per-link
    /// <c>activeClass</c> prop overrides it. Defaults to <c>"router-link-active"</c>.
    /// </summary>
    public string LinkActiveClass { get; set; } = DefaultLinkActiveClass;

    /// <summary>
    /// The default exact-active class applied to a <see cref="RouterLink"/> whose target is the exact
    /// current route (upstream: the <c>linkExactActiveClass</c> router option). A per-link
    /// <c>exactActiveClass</c> prop overrides it. Defaults to <c>"router-link-exact-active"</c>.
    /// </summary>
    public string LinkExactActiveClass { get; set; } = DefaultLinkExactActiveClass;

    /// <summary>Resolves a path to a location through the matcher (upstream: <c>router.resolve</c>).</summary>
    /// <param name="location">The base-stripped location to resolve (path portion).</param>
    /// <returns>The resolved location; an unmatched path yields an empty matched chain.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="location"/> is null.</exception>
    public RouteLocation Resolve(string location)
    {
        ArgumentNullException.ThrowIfNull(location);
        return _matcher.Resolve(location);
    }

    /// <summary>Resolves a named route with interpolated parameters (upstream: <c>router.resolve({ name, params })</c>).</summary>
    /// <param name="name">The route name.</param>
    /// <param name="parameters">The parameter values to interpolate.</param>
    /// <returns>The resolved location.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="parameters"/> is null.</exception>
    /// <exception cref="RouteMatcherException">The name is unknown or a required parameter is missing.</exception>
    public RouteLocation ResolveNamed(string name, RouteParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(parameters);
        return _matcher.ResolveNamed(name, parameters);
    }

    /// <summary>
    /// Builds the anchor <c>href</c> for a location, prefixing the configured base (upstream:
    /// <c>router.resolve(...).href</c> via <c>history.createHref</c>).
    /// </summary>
    /// <param name="location">The base-stripped location.</param>
    /// <returns>The href, base included.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="location"/> is null.</exception>
    public string CreateHref(string location)
    {
        ArgumentNullException.ThrowIfNull(location);
        return _history.CreateHref(location);
    }

    /// <summary>Builds the anchor <c>href</c> for a resolved location's path.</summary>
    /// <param name="location">The resolved location.</param>
    /// <returns>The href, base included.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="location"/> is null.</exception>
    public string CreateHref(RouteLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        return _history.CreateHref(location.Path);
    }

    /// <summary>
    /// Registers a global guard that runs before every navigation (upstream:
    /// <c>router.beforeEach</c>, https://router.vuejs.org/api/#beforeEach). Guards run in registration
    /// order after the leaving guards.
    /// </summary>
    /// <param name="guard">The guard to register.</param>
    /// <returns>A delegate that unregisters <paramref name="guard"/> when invoked (upstream's returned remover).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="guard"/> is null.</exception>
    public Action BeforeEach(NavigationGuard guard) => Add(_beforeGuards, guard);

    /// <summary>
    /// Registers a global guard that runs after per-record and in-component enter guards, just before
    /// a navigation is confirmed (upstream: <c>router.beforeResolve</c>,
    /// https://router.vuejs.org/api/#beforeResolve).
    /// </summary>
    /// <param name="guard">The guard to register.</param>
    /// <returns>A delegate that unregisters <paramref name="guard"/> when invoked.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="guard"/> is null.</exception>
    public Action BeforeResolve(NavigationGuard guard) => Add(_beforeResolveGuards, guard);

    /// <summary>
    /// Registers a hook that runs after every navigation completes or fails (upstream:
    /// <c>router.afterEach</c>). It receives the resolved <see cref="NavigationFailure"/> (or
    /// <see langword="null"/> on success) and cannot change the outcome.
    /// </summary>
    /// <param name="hook">The hook to register.</param>
    /// <returns>A delegate that unregisters <paramref name="hook"/> when invoked.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hook"/> is null.</exception>
    public Action AfterEach(AfterNavigationHook hook) => Add(_afterHooks, hook);

    /// <summary>
    /// Registers a handler for unexpected exceptions thrown by guards during navigation (upstream:
    /// <c>router.onError</c>, https://router.vuejs.org/api/#onError). Navigation
    /// <see cref="NavigationFailure"/>s are returned from <see cref="Push"/> rather than routed here.
    /// </summary>
    /// <param name="handler">The error handler to register.</param>
    /// <returns>A delegate that unregisters <paramref name="handler"/> when invoked.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is null.</exception>
    public Action OnError(NavigationErrorHandler handler) => Add(_errorHandlers, handler);

    /// <summary>
    /// Navigates to <paramref name="location"/> through the guard pipeline, pushing a new history
    /// entry and updating <see cref="CurrentRoute"/> on success (upstream: <c>router.push</c>). The
    /// returned task resolves with <see langword="null"/> on success or a <see cref="NavigationFailure"/>
    /// when the navigation is aborted, cancelled, or duplicated; it faults only on an unexpected guard
    /// exception (also routed to <see cref="OnError"/>).
    /// </summary>
    /// <param name="location">The base-stripped location to navigate to.</param>
    /// <returns>The navigation outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="location"/> is null.</exception>
    public Task<NavigationFailure?> Push(string location) => Navigate(location, replace: false);

    /// <summary>
    /// Navigates to <paramref name="location"/> through the guard pipeline, replacing the current
    /// history entry and updating <see cref="CurrentRoute"/> on success (upstream:
    /// <c>router.replace</c>). Resolves with the same navigation result as <see cref="Push"/>.
    /// </summary>
    /// <param name="location">The base-stripped location to navigate to.</param>
    /// <returns>The navigation outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="location"/> is null.</exception>
    public Task<NavigationFailure?> Replace(string location) => Navigate(location, replace: true);

    /// <summary>
    /// Runs the initial navigation and resolves when it settles — the C# port of vue-router's
    /// <c>router.isReady()</c> combined with the install-time initial push
    /// (<c>packages/router/src/router.ts</c>, https://router.vuejs.org/api/#isReady). The first call
    /// navigates to the current history location through the <b>full</b> guard pipeline with
    /// <c>from</c> = <see cref="RouteLocation.Start"/>, so a global <see cref="BeforeEach"/> redirect
    /// (the classic <c>/</c> → <c>/x</c>) fires even for a page loaded directly at that URL; the
    /// confirm step replaces the current history entry rather than pushing a new one. Idempotent —
    /// every call returns the same task, so the initial navigation runs exactly once.
    /// </summary>
    /// <remarks>
    /// A Viu bootstrap awaits this before mounting so the first render already reflects the resolved
    /// (or redirected) route. The returned task resolves with the initial navigation's
    /// <see cref="NavigationFailure"/> (or <see langword="null"/> on success) and faults only on an
    /// unexpected guard exception. Because there is no <c>app.use(router)</c> install hook in Viu, this
    /// single method folds upstream's separate install-time trigger and <c>isReady()</c> await; unlike
    /// upstream's <c>isReady()</c>, it always settles (never hangs) for an aborted initial navigation.
    /// </remarks>
    /// <returns>The initial navigation outcome, settling when it completes.</returns>
    /// <exception cref="ObjectDisposedException">The router has been disposed.</exception>
    public Task<NavigationFailure?> ReadyAsync()
    {
        ThrowIfDisposed();
        return _initialNavigation ??= Navigate(_history.Location, replace: false);
    }

    /// <summary>
    /// Moves through the history stack by <paramref name="delta"/> entries, driving the same guard
    /// pipeline as a browser back/forward (upstream: <c>router.go</c>). The resulting navigation runs
    /// asynchronously through the history listener.
    /// </summary>
    /// <param name="delta">The signed number of entries to move (negative is backward).</param>
    public void Go(int delta)
    {
        ThrowIfDisposed();
        _history.Go(delta);
    }

    /// <summary>Moves one entry back in history (upstream: <c>router.back</c>).</summary>
    public void Back() => Go(-1);

    /// <summary>Moves one entry forward in history (upstream: <c>router.forward</c>).</summary>
    public void Forward() => Go(1);

    /// <summary>Removes the history listener and cancels any in-flight navigation so nothing outlives the router.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _unlisten();
        _pendingNavigation?.Cancel();
        _pendingNavigation?.Dispose();
        _pendingNavigation = null;
    }

    // Registers an in-component leave guard for a record and returns its remover (called by
    // RouterGuards.OnBeforeRouteLeave, bound to the component's onUnmounted).
    internal Action RegisterLeaveGuard(RouteRecord record, NavigationGuard guard)
        => AddRecordGuard(_leaveGuards, record, guard);

    // Registers an in-component update guard for a record and returns its remover.
    internal Action RegisterUpdateGuard(RouteRecord record, NavigationGuard guard)
        => AddRecordGuard(_updateGuards, record, guard);

    private static Action Add<T>(List<T> list, T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        list.Add(item);
        return () => list.Remove(item);
    }

    private static Action AddRecordGuard(
        Dictionary<RouteRecord, List<NavigationGuard>> table,
        RouteRecord record,
        NavigationGuard guard)
    {
        if (!table.TryGetValue(record, out var guards))
        {
            guards = [];
            table[record] = guards;
        }
        guards.Add(guard);
        return () =>
        {
            if (table.TryGetValue(record, out var current) && current.Remove(guard) && current.Count == 0)
            {
                table.Remove(record);
            }
        };
    }

    private async Task<NavigationFailure?> Navigate(string location, bool replace)
    {
        ArgumentNullException.ThrowIfNull(location);
        ThrowIfDisposed();
        var to = _matcher.Resolve(location);
        try
        {
            return await PushWithRedirect(to, replace, redirectedFrom: null, redirectCount: 0);
        }
        catch (Exception exception)
        {
            // Unexpected guard exception (or an infinite-redirect abort): route it to onError and let
            // the task fault, mirroring vue-router's triggerError + promise rejection.
            TriggerError(exception, to, _currentRoute.Value);
            throw;
        }
    }

    // Upstream pushWithRedirect: resolve, run the pipeline, then confirm / abort / cancel / recurse on
    // redirect. from is the current route for the whole chain (no confirm happens until the end).
    private async Task<NavigationFailure?> PushWithRedirect(
        RouteLocation to,
        bool replace,
        RouteLocation? redirectedFrom,
        int redirectCount)
    {
        var token = BeginNavigation();
        var from = _currentRoute.Value;
        NavigationFailure? failure;
        // Dedup only when `from` already has a matched chain (upstream gates on `from.matched.length`):
        // the START sentinel has an empty chain, so the initial navigation is never deduplicated and
        // always runs the full pipeline, while in-session same-location navigations still short-circuit.
        if (from.Matched.Count > 0 && IsSameLocation(from, to))
        {
            // Duplicated: skip the pipeline entirely but still notify afterEach (upstream parity).
            failure = new NavigationFailure(NavigationFailureType.Duplicated, to, from);
        }
        else
        {
            var outcome = await RunNavigationPipeline(to, from, token);
            switch (outcome.Kind)
            {
                case NavigationOutcomeKind.Redirect:
                    if (redirectCount >= MaximumRedirectDepth)
                    {
                        throw NavigationRedirectException.LoopExceeded(from, to, redirectCount);
                    }
                    var redirectTarget = ResolveRedirectTarget(outcome.Redirect!);
                    // afterEach fires for the final navigation only (upstream: the redirect recurses
                    // before triggerAfterEach), so return the recursion's result directly.
                    return await PushWithRedirect(redirectTarget, replace, redirectedFrom ?? to, redirectCount + 1);
                case NavigationOutcomeKind.Abort:
                    failure = new NavigationFailure(NavigationFailureType.Aborted, to, from);
                    break;
                case NavigationOutcomeKind.Cancel:
                    failure = new NavigationFailure(NavigationFailureType.Cancelled, to, from);
                    break;
                default:
                    // Allow: a final supersession check (a newer navigation may have started during
                    // the last guard's await) before we commit anything.
                    if (token.IsCancellationRequested)
                    {
                        failure = new NavigationFailure(NavigationFailureType.Cancelled, to, from);
                    }
                    else
                    {
                        // The first navigation (from the START sentinel) replaces the current history
                        // entry rather than pushing a new one, so the app's entry URL is not left as a
                        // stale back-target (upstream: isFirstNavigation forces a replace in
                        // finalizeNavigation). ReferenceEquals stays true through a redirect chain
                        // because nothing is committed until this confirm.
                        var isFirstNavigation = ReferenceEquals(from, RouteLocation.Start);
                        FinalizeNavigation(to, isPush: true, replace || isFirstNavigation);
                        failure = null;
                    }
                    break;
            }
        }
        TriggerAfterEach(to, from, failure);
        return failure;
    }

    // Upstream navigate(): the ordered guard queue. Each phase runs its guards in order; the first
    // non-allow decision short-circuits. Cancellation is re-checked at the head of every phase and
    // once more before finalize, so a superseded chain runs no further guards.
    private async Task<NavigationOutcome> RunNavigationPipeline(RouteLocation to, RouteLocation from, CancellationToken token)
    {
        // 1. beforeRouteLeave on leaving records (deepest child first).
        var outcome = await RunPhase(CollectLeaveGuards(from, to), to, from, token);
        if (!outcome.IsAllow)
        {
            return outcome;
        }
        // 2. global beforeEach.
        outcome = await RunPhase(Snapshot(_beforeGuards), to, from, token);
        if (!outcome.IsAllow)
        {
            return outcome;
        }
        // 3. beforeRouteUpdate on reused (updating) records.
        outcome = await RunPhase(CollectUpdateGuards(from, to), to, from, token);
        if (!outcome.IsAllow)
        {
            return outcome;
        }
        // 4. per-record beforeEnter on entering records.
        outcome = await RunPhase(CollectBeforeEnterGuards(from, to), to, from, token);
        if (!outcome.IsAllow)
        {
            return outcome;
        }
        // 4.5. Resolve async route components — a no-op seam until [V01.01.03.16]; every route
        // component is eager today. The stage is preserved so the ordering stays faithful to
        // vue-router (beforeEnter -> resolve components -> beforeRouteEnter).
        // 5. in-component beforeRouteEnter on entering records.
        outcome = await RunPhase(CollectRouteEnterGuards(from, to), to, from, token);
        if (!outcome.IsAllow)
        {
            return outcome;
        }
        // 6. global beforeResolve.
        return await RunPhase(Snapshot(_beforeResolveGuards), to, from, token);
    }

    private static async Task<NavigationOutcome> RunPhase(
        IReadOnlyList<NavigationGuard> guards,
        RouteLocation to,
        RouteLocation from,
        CancellationToken token)
    {
        // Head-of-phase supersession check covers a zero-guard phase and a supersession during the
        // previous phase's final await.
        if (token.IsCancellationRequested)
        {
            return NavigationOutcome.Cancel;
        }
        foreach (var guard in guards)
        {
            var result = await guard(to, from, token);
            if (token.IsCancellationRequested)
            {
                return NavigationOutcome.Cancel;
            }
            switch (result.Action)
            {
                case NavigationGuardAction.Abort:
                    return NavigationOutcome.Abort;
                case NavigationGuardAction.Redirect:
                    return NavigationOutcome.Redirecting(result);
                default:
                    continue;
            }
        }
        return NavigationOutcome.Allow;
    }

    // Leaving records = in `from` but not `to`, deepest child first (upstream leavingRecords.reverse()).
    private List<NavigationGuard> CollectLeaveGuards(RouteLocation from, RouteLocation to)
    {
        var guards = new List<NavigationGuard>();
        for (var index = from.Matched.Count - 1; index >= 0; index--)
        {
            var record = from.Matched[index];
            if (!ContainsRecord(to.Matched, record) && _leaveGuards.TryGetValue(record, out var registered))
            {
                guards.AddRange(registered);
            }
        }
        return guards;
    }

    // Updating records = in both `from` and `to` (reused), parent to child.
    private List<NavigationGuard> CollectUpdateGuards(RouteLocation from, RouteLocation to)
    {
        var guards = new List<NavigationGuard>();
        foreach (var record in from.Matched)
        {
            if (ContainsRecord(to.Matched, record) && _updateGuards.TryGetValue(record, out var registered))
            {
                guards.AddRange(registered);
            }
        }
        return guards;
    }

    // Entering records = in `to` but not `from`, parent to child; each record's beforeEnter guard.
    private static List<NavigationGuard> CollectBeforeEnterGuards(RouteLocation from, RouteLocation to)
    {
        var guards = new List<NavigationGuard>();
        foreach (var record in to.Matched)
        {
            if (record.BeforeEnter is { } guard && !ContainsRecord(from.Matched, record))
            {
                guards.Add(guard);
            }
        }
        return guards;
    }

    // Entering records whose component contributes a beforeRouteEnter guard (interface-based
    // discovery, no reflection; the component instance does not yet exist).
    private static List<NavigationGuard> CollectRouteEnterGuards(RouteLocation from, RouteLocation to)
    {
        var guards = new List<NavigationGuard>();
        foreach (var record in to.Matched)
        {
            if (record.Component is IRouteEnterGuard enterGuard && !ContainsRecord(from.Matched, record))
            {
                guards.Add(enterGuard.BeforeRouteEnter);
            }
        }
        return guards;
    }

    private static bool ContainsRecord(IReadOnlyList<RouteRecord> records, RouteRecord record)
    {
        foreach (var candidate in records)
        {
            if (ReferenceEquals(candidate, record))
            {
                return true;
            }
        }
        return false;
    }

    private static IReadOnlyList<T> Snapshot<T>(List<T> list) => list.Count == 0 ? Array.Empty<T>() : [.. list];

    private RouteLocation ResolveRedirectTarget(NavigationGuardResult redirect)
        => redirect.RedirectName is { } name
            ? _matcher.ResolveNamed(name, redirect.RedirectParameters ?? RouteParameters.Empty)
            : _matcher.Resolve(redirect.RedirectLocation!);

    // Commits a confirmed navigation: write history (push/replace) for an application navigation, then
    // set CurrentRoute (one shallowRef trigger). A pop leaves history alone — the URL already moved —
    // and only updates CurrentRoute. The trigger queues the render flush; it is deliberately committed
    // before afterEach runs and before the render-phase flush (Vue's microtask ordering).
    private void FinalizeNavigation(RouteLocation to, bool isPush, bool replace)
    {
        if (isPush)
        {
            if (replace)
            {
                _history.Replace(to.Path);
            }
            else
            {
                _history.Push(to.Path);
            }
        }
        _currentRoute.Value = to;
    }

    private void TriggerAfterEach(RouteLocation to, RouteLocation from, NavigationFailure? failure)
    {
        foreach (var hook in Snapshot(_afterHooks))
        {
            try
            {
                hook(to, from, failure);
            }
            catch (Exception exception)
            {
                // An afterEach hook throwing must not undo a completed navigation; route it to the
                // error handlers (a small robustness divergence from upstream's un-guarded forEach).
                TriggerError(exception, to, from);
            }
        }
    }

    private void TriggerError(Exception error, RouteLocation to, RouteLocation from)
    {
        foreach (var handler in Snapshot(_errorHandlers))
        {
            handler(error, to, from);
        }
    }

    // Supersede any in-flight navigation and start a fresh cancellation scope for this one. A
    // superseded token reads cancelled at its next checkpoint; the source is left for GC (it holds no
    // unmanaged resource) and only the current one is disposed on Dispose().
    private CancellationToken BeginNavigation()
    {
        _pendingNavigation?.Cancel();
        _pendingNavigation = new CancellationTokenSource();
        return _pendingNavigation.Token;
    }

    private static bool IsSameLocation(RouteLocation from, RouteLocation to) => from.Equals(to);

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    // Browser back/forward and memory Go route through the history listener. They drive the same
    // guarded pipeline as an application navigation, but the URL has already moved, so a failure
    // restores it with a compensating history.go (upstream's popstate handling). Fire-and-forget: the
    // async body catches everything, so its task never faults.
    private void OnHistoryNavigation(string to, string from, NavigationInformation information)
        => _ = HandlePopNavigation(to, information);

    private async Task HandlePopNavigation(string location, NavigationInformation information)
    {
        if (_disposed)
        {
            return;
        }
        var to = _matcher.Resolve(location);
        var from = _currentRoute.Value;
        var token = BeginNavigation();
        var urlRestored = false;
        NavigationFailure? failure;
        try
        {
            if (IsSameLocation(from, to))
            {
                failure = new NavigationFailure(NavigationFailureType.Duplicated, to, from);
            }
            else
            {
                var outcome = await RunNavigationPipeline(to, from, token);
                switch (outcome.Kind)
                {
                    case NavigationOutcomeKind.Redirect:
                        // Restore the popped URL, then re-navigate to the redirect target as a push
                        // (upstream: routerHistory.go(-delta, false) + pushWithRedirect).
                        RestorePopLocation(information);
                        urlRestored = true;
                        var redirectTarget = ResolveRedirectTarget(outcome.Redirect!);
                        await PushWithRedirect(redirectTarget, replace: false, redirectedFrom: to, redirectCount: 0);
                        return;
                    case NavigationOutcomeKind.Abort:
                        failure = new NavigationFailure(NavigationFailureType.Aborted, to, from);
                        break;
                    case NavigationOutcomeKind.Cancel:
                        failure = new NavigationFailure(NavigationFailureType.Cancelled, to, from);
                        break;
                    default:
                        if (token.IsCancellationRequested)
                        {
                            failure = new NavigationFailure(NavigationFailureType.Cancelled, to, from);
                        }
                        else
                        {
                            FinalizeNavigation(to, isPush: false, replace: false);
                            failure = null;
                        }
                        break;
                }
            }
        }
        catch (Exception exception)
        {
            // Restore only if a redirect leg has not already done so, to avoid a double compensating go.
            if (!urlRestored)
            {
                RestorePopLocation(information);
            }
            TriggerError(exception, to, from);
            return;
        }
        if (failure is not null)
        {
            // Aborted / cancelled / duplicated pop: undo the browser's position change so the URL
            // matches the untouched CurrentRoute (upstream's compensating history.go).
            RestorePopLocation(information);
        }
        TriggerAfterEach(to, from, failure);
    }

    private void RestorePopLocation(NavigationInformation information)
    {
        if (information.Delta != 0)
        {
            _history.Go(-information.Delta, triggerListeners: false);
        }
    }
}
