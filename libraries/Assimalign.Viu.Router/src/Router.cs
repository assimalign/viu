using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.Router;

/// <summary>
/// The router instance the built-in components consume. It owns the reactive
/// <see cref="CurrentRoute"/> — a shallow reference over the resolved location, so a navigation is
/// one trigger rather than one per changed field — resolves targets and hrefs through the matcher
/// and history, and drives navigations through the asynchronous, guarded, cancellable pipeline with
/// <see cref="Push"/>/<see cref="Replace"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The navigation pipeline</b> runs guards in a fixed order — in-component before-leave (deepest
/// child first) → global <see cref="BeforeEach"/> → reused-record before-update → per-record
/// <see cref="RouteRecord.BeforeEnter"/> → (async component resolution, currently a no-op seam for
/// <c>[V01.01.03.16]</c>) → in-component <see cref="IRouteEnterGuard"/> → global
/// <see cref="BeforeResolve"/> → confirm (history write +
/// <see cref="CurrentRoute"/> update, one trigger) → <see cref="AfterEach"/>. A guard may allow,
/// abort, or redirect; a redirect re-enters the pipeline (with infinite-redirect protection), an
/// abort leaves <see cref="CurrentRoute"/> and history untouched, and a later navigation cancels an
/// in-flight one through a cooperative <see cref="CancellationToken"/>.
/// </para>
/// <para>
/// <b>The initial navigation</b> starts from a sentinel, not from a pre-resolved route:
/// <see cref="CurrentRoute"/> begins at <see cref="RouteLocation.Start"/> (empty matched chain).
/// <see cref="ReadyAsync"/> runs the first navigation to the history location through the same full
/// pipeline with <c>from</c> = that sentinel, so a global <see cref="BeforeEach"/> redirect fires for
/// a direct page load, and its confirm step replaces (never pushes) the current history entry — a
/// push would leave a bogus back target pointing at the URL the user actually opened. The
/// same-location dedup is skipped whenever <c>from</c> has an empty matched chain, so the sentinel
/// never short-circuits the initial pass while in-session duplicates still do.
/// </para>
/// <para>
/// <b>Three deliberate API shapes</b> (see <c>docs/DESIGN.md</c>): a guard returns a
/// <see cref="NavigationGuardResult"/> rather than invoking a continuation, so it decides exactly
/// once (<c>[RTR-5]</c>); <see cref="Push"/> completes with a <see cref="NavigationFailure"/> for
/// abort/cancel/duplicate and faults only on a genuinely unexpected guard exception (routed to
/// <see cref="OnError"/>), keeping routine outcomes out of exception control flow; and a redirect
/// loop throws <see cref="NavigationRedirectException"/> in every configuration, not only in
/// development builds.
/// </para>
/// <para>Not thread-safe: the router targets the single-threaded JS event loop.</para>
/// <para>
/// Specified by <c>[RTR-3]</c>, <c>[RTR-5]</c>, <c>[RTR-6]</c>, and <c>[RTR-7]</c>.
/// </para>
/// </remarks>
public sealed class Router : IDisposable
{
    private const string DefaultLinkActiveClass = "router-link-active";
    private const string DefaultLinkExactActiveClass = "router-link-exact-active";

    // The redirect-depth safety cap, enforced in every configuration. Kept well above any legitimate
    // redirect chain so only a genuine loop trips it.
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
        // being deduplicated against a pre-resolved route: the initial navigation is a real pass
        // through the pipeline from the sentinel ([V01.01.08.07]).
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
    /// The reactive current location, held in a shallow reference: one trigger per completed
    /// navigation drives every <see cref="RouterView"/> and the active-class computation of every
    /// <see cref="RouterLink"/> through the normal reactivity graph.
    /// </summary>
    public IReactiveReference<RouteLocation> CurrentRoute => _currentRoute;

    /// <summary>
    /// The default active class applied to a <see cref="RouterLink"/> whose target is an inclusive
    /// match of the current route. A per-link <c>activeClass</c> argument overrides it. Defaults to
    /// <c>"router-link-active"</c>.
    /// </summary>
    public string LinkActiveClass { get; set; } = DefaultLinkActiveClass;

    /// <summary>
    /// The default exact-active class applied to a <see cref="RouterLink"/> whose target is the exact
    /// current route. A per-link <c>exactActiveClass</c> argument overrides it. Defaults to
    /// <c>"router-link-exact-active"</c>.
    /// </summary>
    public string LinkExactActiveClass { get; set; } = DefaultLinkExactActiveClass;

    /// <summary>Resolves a path to a location through the matcher.</summary>
    /// <param name="location">The base-stripped location to resolve (path portion).</param>
    /// <returns>The resolved location; an unmatched path yields an empty matched chain.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="location"/> is null.</exception>
    public RouteLocation Resolve(string location)
    {
        ArgumentNullException.ThrowIfNull(location);
        return _matcher.Resolve(location);
    }

    /// <summary>Resolves a named route with interpolated parameters.</summary>
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
    /// Builds the anchor <c>href</c> for a location, prefixing the configured base.
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
    /// Registers a global guard that runs before every navigation. Guards run in registration order,
    /// after the in-component leaving guards.
    /// </summary>
    /// <param name="guard">The guard to register.</param>
    /// <returns>A delegate that unregisters <paramref name="guard"/> when invoked.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="guard"/> is null.</exception>
    public Action BeforeEach(NavigationGuard guard) => Add(_beforeGuards, guard);

    /// <summary>
    /// Registers a global guard that runs after per-record and in-component enter guards, just before
    /// a navigation is confirmed — the last chance to abort or redirect.
    /// </summary>
    /// <param name="guard">The guard to register.</param>
    /// <returns>A delegate that unregisters <paramref name="guard"/> when invoked.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="guard"/> is null.</exception>
    public Action BeforeResolve(NavigationGuard guard) => Add(_beforeResolveGuards, guard);

    /// <summary>
    /// Registers a hook that runs after every navigation completes or fails. It receives the resolved
    /// <see cref="NavigationFailure"/> (or <see langword="null"/> on success) and cannot change the
    /// outcome.
    /// </summary>
    /// <param name="hook">The hook to register.</param>
    /// <returns>A delegate that unregisters <paramref name="hook"/> when invoked.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hook"/> is null.</exception>
    public Action AfterEach(AfterNavigationHook hook) => Add(_afterHooks, hook);

    /// <summary>
    /// Registers a handler for unexpected exceptions thrown by guards during navigation. A
    /// <see cref="NavigationFailure"/> is returned from <see cref="Push"/> rather than routed here.
    /// </summary>
    /// <param name="handler">The error handler to register.</param>
    /// <returns>A delegate that unregisters <paramref name="handler"/> when invoked.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is null.</exception>
    public Action OnError(NavigationErrorHandler handler) => Add(_errorHandlers, handler);

    /// <summary>
    /// Navigates to <paramref name="location"/> through the guard pipeline, pushing a new history
    /// entry and updating <see cref="CurrentRoute"/> on success. The
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
    /// history entry and updating <see cref="CurrentRoute"/> on success. Resolves with the same
    /// navigation result as <see cref="Push"/>.
    /// </summary>
    /// <param name="location">The base-stripped location to navigate to.</param>
    /// <returns>The navigation outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="location"/> is null.</exception>
    public Task<NavigationFailure?> Replace(string location) => Navigate(location, replace: true);

    /// <summary>
    /// Runs the initial navigation and resolves when it settles. The first call
    /// navigates to the current history location through the <b>full</b> guard pipeline with
    /// <c>from</c> = <see cref="RouteLocation.Start"/>, so a global <see cref="BeforeEach"/> redirect
    /// (the classic <c>/</c> → <c>/x</c>) fires even for a page loaded directly at that URL; the
    /// confirm step replaces the current history entry rather than pushing a new one. Idempotent —
    /// every call returns the same task, so the initial navigation runs exactly once. The first
    /// call's cancellation token owns bridge initialization and that navigation; tokens supplied by
    /// later callers cannot replace it.
    /// </summary>
    /// <remarks>
    /// A Viu bootstrap awaits this before mounting so the first render already reflects the resolved
    /// (or redirected) route. After history initialization, the returned task resolves with the
    /// initial navigation's <see cref="NavigationFailure"/> (or <see langword="null"/> on success);
    /// caller cancellation during a guard produces a cancelled failure, while cancellation or failure
    /// during history initialization propagates from this task. An unexpected guard exception also
    /// faults it. Viu has no separate router-install hook, so triggering the first navigation and
    /// awaiting it are one method rather than two, and an aborted initial navigation completes with
    /// its failure instead of hanging.
    /// </remarks>
    /// <param name="cancellationToken">Cancels history initialization and the initial navigation.</param>
    /// <returns>The initial navigation outcome, settling when it completes.</returns>
    /// <exception cref="ObjectDisposedException">The router has been disposed.</exception>
    public Task<NavigationFailure?> ReadyAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _initialNavigation ??= ReadyCoreAsync(cancellationToken);
    }

    /// <summary>
    /// Moves through the history stack by <paramref name="delta"/> entries, driving the same guard
    /// pipeline as a browser back/forward. The resulting navigation runs asynchronously through the
    /// history listener, so this method returns before it settles.
    /// </summary>
    /// <param name="delta">The signed number of entries to move (negative is backward).</param>
    public void Go(int delta)
    {
        ThrowIfDisposed();
        _history.Go(delta);
    }

    /// <summary>Moves one entry back in history.</summary>
    public void Back() => Go(-1);

    /// <summary>Moves one entry forward in history.</summary>
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

    private async Task<NavigationFailure?> ReadyCoreAsync(
        CancellationToken cancellationToken)
    {
        if (_history is IInitializableRouterHistory initializableHistory)
        {
            await initializableHistory
                .InitializeAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return await Navigate(
            _history.Location,
            replace: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<NavigationFailure?> Navigate(
        string location,
        bool replace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(location);
        ThrowIfDisposed();
        var to = _matcher.Resolve(location);
        try
        {
            return await PushWithRedirect(
                to,
                replace,
                redirectedFrom: null,
                redirectCount: 0,
                cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            // Unexpected guard exception (or an infinite-redirect abort): route it to the error
            // handlers and let the task fault.
            TriggerError(exception, to, _currentRoute.Value);
            throw;
        }
    }

    // Resolve, run the pipeline, then confirm / abort / cancel / recurse on
    // redirect. from is the current route for the whole chain (no confirm happens until the end).
    private async Task<NavigationFailure?> PushWithRedirect(
        RouteLocation to,
        bool replace,
        RouteLocation? redirectedFrom,
        int redirectCount,
        CancellationToken cancellationToken)
    {
        var token = BeginNavigation(cancellationToken);
        var from = _currentRoute.Value;
        NavigationFailure? failure;
        // Dedup only when `from` already has a matched chain:
        // the START sentinel has an empty chain, so the initial navigation is never deduplicated and
        // always runs the full pipeline, while in-session same-location navigations still short-circuit.
        if (from.Matched.Count > 0 && IsSameLocation(from, to))
        {
            // Duplicated: skip the pipeline entirely but still notify afterEach, so a hook that
            // the navigation state stays in sync even for a no-op navigation.
            failure = new NavigationFailure(NavigationFailureType.Duplicated, to, from);
        }
        else
        {
            NavigationOutcome outcome;
            try
            {
                outcome = await RunNavigationPipeline(to, from, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                failure = new NavigationFailure(
                    NavigationFailureType.Cancelled,
                    to,
                    from);
                TriggerAfterEach(to, from, failure);
                return failure;
            }

            switch (outcome.Kind)
            {
                case NavigationOutcomeKind.Redirect:
                    if (redirectCount >= MaximumRedirectDepth)
                    {
                        throw NavigationRedirectException.LoopExceeded(from, to, redirectCount);
                    }
                    var redirectTarget = ResolveRedirectTarget(outcome.Redirect!);
                    // afterEach fires for the final navigation only — the redirect recurses before
                    // the after-hooks run — so return the recursion's result directly.
                    return await PushWithRedirect(
                        redirectTarget,
                        replace,
                        redirectedFrom ?? to,
                        redirectCount + 1,
                        cancellationToken);
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
                        // stale back-target. ReferenceEquals stays true through a redirect chain
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

    // The ordered guard queue. Each phase runs its guards in order; the first
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
        // component is eager today. The stage is kept so the ordering does not shift when lazy
        // components land: before-enter -> resolve components -> in-component before-enter.
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

    // Leaving records = in `from` but not `to`, deepest child first.
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

    // Entering records with an explicitly registered component-associated beforeRouteEnter guard.
    // The component instance does not yet exist, so route registration owns this metadata.
    private static List<NavigationGuard> CollectRouteEnterGuards(RouteLocation from, RouteLocation to)
    {
        var guards = new List<NavigationGuard>();
        foreach (var record in to.Matched)
        {
            if (record.RouteEnterGuard is { } enterGuard
                && !ContainsRecord(from.Matched, record))
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
    // set CurrentRoute (one shallow-reference trigger). A pop leaves history alone — the URL already
    // moved — and only updates CurrentRoute. The trigger queues the render flush; it is deliberately
    // committed before afterEach runs and before the render-phase flush, so a hook observes the new
    // route but the DOM has not been touched yet.
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
                // error handlers — one misbehaving hook must not strand the rest.
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
    // caller token is linked rather than substituted so later navigations can still supersede it.
    // The current source is disposed by Dispose; a superseded source remains alive while its guard
    // still holds the token and becomes collectible when that navigation settles.
    private CancellationToken BeginNavigation(
        CancellationToken cancellationToken = default)
    {
        _pendingNavigation?.Cancel();
        _pendingNavigation = cancellationToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : new CancellationTokenSource();
        return _pendingNavigation.Token;
    }

    private static bool IsSameLocation(RouteLocation from, RouteLocation to) => from.Equals(to);

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    // Browser back/forward and memory Go route through the history listener. They drive the same
    // guarded pipeline as an application navigation, but the URL has already moved, so a failure
    // restores it with a compensating history.go. Fire-and-forget: the
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
                        // (a silent go back by -delta, then the redirected navigation).
                        RestorePopLocation(information);
                        urlRestored = true;
                        var redirectTarget = ResolveRedirectTarget(outcome.Redirect!);
                        await PushWithRedirect(
                            redirectTarget,
                            replace: false,
                            redirectedFrom: to,
                            redirectCount: 0,
                            cancellationToken: default);
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
            // matches the untouched CurrentRoute.
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
