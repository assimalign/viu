using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Router;

namespace Assimalign.Viu.Browser.Router;

/// <summary>
/// The browser history <em>policy</em>: it owns the current location and state, composes URLs from
/// the configured base, runs the push/replace/popstate state machine, and books navigation
/// listeners — all without touching the DOM. Every environment effect is delegated to an injected
/// <see cref="IBrowserHistoryInterop"/>. Web and hash modes share this class; they differ only in
/// the base handed to it — a hash base carries a <c>#</c> — so there is one state machine to reason
/// about and test rather than two.
/// </summary>
/// <remarks>
/// Because the browser edge is an injected seam, the whole policy — base prepend/strip, the state
/// round-trip, the <c>popstate</c> delta/direction computation, listener bookkeeping, and the
/// single-crossing batched reads/writes — is unit-testable with a fake interop and no browser. Not
/// thread-safe (single-threaded JS event-loop model).
/// </remarks>
internal sealed class BrowserRouterHistoryImplementation :
    IRouterHistory,
    IRouterScrollController
{
    private readonly IBrowserHistoryInterop interop;
    private readonly List<NavigationCallback> listeners = [];
    private readonly string normalizedBase;
    private readonly int subscriptionIdentifier;

    private string currentLocation;
    private RouterHistoryState currentState;
    private bool isDisposed;
    // The location a silent Go() is leaving, so the popstate it provokes is swallowed.
    private string? pausedLocation;

    private PendingScrollRequest? _pendingInitialScroll;

    internal BrowserRouterHistoryImplementation(
        IBrowserHistoryInterop interop,
        string normalizedBase)
    {
        this.interop = interop;
        this.normalizedBase = normalizedBase;

        var snapshot = interop.ReadSnapshot();
        currentLocation = BrowserHistoryPathNormalization.CreateCurrentLocation(
            Base, snapshot.Pathname, snapshot.Search, snapshot.Hash);

        bool mustSeedCurrentEntry = snapshot.State is null;
        currentState = snapshot.State
            ?? BrowserRouterHistoryStateBuilder.BuildInitial(
                currentLocation,
                snapshot.HistoryLength - 1);

        // One popstate listener and one saved-position ledger per history instance. Register before
        // a possible bootstrap replace so that mutation is scoped to this exact subscription.
        int registeredSubscriptionIdentifier = interop.Subscribe(OnPopState);
        try
        {
            if (mustSeedCurrentEntry)
            {
                // Fresh navigation with no prior state: seed the current entry from the
                // environment's history length and replace it in place, so bootstrapping never
                // adds an entry.
                interop.Replace(
                    registeredSubscriptionIdentifier,
                    BuildUrl(currentLocation),
                    currentState);
            }
        }
        catch
        {
            interop.Unsubscribe(registeredSubscriptionIdentifier);
            throw;
        }
        subscriptionIdentifier = registeredSubscriptionIdentifier;
    }

    /// <inheritdoc/>
    public string Base
    {
        get
        {
            ThrowIfDisposed();
            return normalizedBase;
        }
    }

    /// <inheritdoc/>
    public string Location
    {
        get
        {
            ThrowIfDisposed();
            return currentLocation;
        }
    }

    /// <inheritdoc/>
    public RouterHistoryState State
    {
        get
        {
            ThrowIfDisposed();
            return currentState;
        }
    }

    /// <inheritdoc/>
    public void Push(string location, RouterHistoryEntryOptions options = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(location);
        // Rewrite the leaving entry to point forward at `location` (the interop fills its live scroll
        // anchor), then push the new entry — both in a single interop crossing.
        var amendedCurrent = currentState with { Forward = location, Scroll = null };
        var newState = BrowserRouterHistoryStateBuilder.BuildForPush(
            currentState,
            location,
            options.Scroll);
        interop.Push(
            subscriptionIdentifier,
            BuildUrl(currentLocation),
            amendedCurrent,
            BuildUrl(location),
            newState);
        currentState = newState;
        currentLocation = location;
    }

    /// <inheritdoc/>
    public void Replace(string location, RouterHistoryEntryOptions options = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(location);
        var newState = BrowserRouterHistoryStateBuilder.BuildForReplace(
            currentState,
            location,
            options.Scroll);
        interop.Replace(subscriptionIdentifier, BuildUrl(location), newState);
        currentState = newState;
        currentLocation = location;
    }

    /// <inheritdoc/>
    public void Go(
        int delta,
        RouterHistoryNavigationOptions options = RouterHistoryNavigationOptions.None)
    {
        ThrowIfDisposed();
        if ((options & RouterHistoryNavigationOptions.SuppressListeners) != 0)
        {
            // Swallow the popstate this Go() will provoke, so a silent reposition is invisible.
            pausedLocation = currentLocation;
        }
        interop.Go(delta);
    }

    /// <inheritdoc/>
    public Action Listen(NavigationCallback callback)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(callback);
        listeners.Add(callback);
        return () => listeners.Remove(callback);
    }

    /// <inheritdoc/>
    public string CreateHref(string location)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(location);
        return BrowserHistoryPathNormalization.CreateHref(Base, location);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        _pendingInitialScroll = null;
        listeners.Clear();
        interop.Unsubscribe(subscriptionIdentifier);
    }

    /// <inheritdoc/>
    public Task ApplyAsync(
        RouteLocation to,
        RouteLocation from,
        ScrollPosition? savedPosition,
        ScrollBehavior? behavior,
        bool isInitialNavigation,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        // Every newer confirmation supersedes initial work parked before mount, even when the
        // current Router.ScrollBehavior is null. The confirmation signal itself owns invalidation.
        _pendingInitialScroll = null;
        if (behavior is null)
        {
            return Task.CompletedTask;
        }

        PendingScrollRequest request = new(
            to,
            from,
            savedPosition,
            behavior,
            cancellationToken);
        if (isInitialNavigation)
        {
            _pendingInitialScroll = request;
            return Task.CompletedTask;
        }

        return ApplyScrollAsync(request);
    }

    /// <inheritdoc/>
    public Task CompleteInitialScrollAsync()
    {
        ThrowIfDisposed();
        PendingScrollRequest? request = _pendingInitialScroll;
        _pendingInitialScroll = null;
        return request is null
            ? Task.CompletedTask
            : ApplyScrollAsync(request.Value);
    }

    private async Task ApplyScrollAsync(PendingScrollRequest request)
    {
        await Scheduler.NextTickAsync().ConfigureAwait(false);
        if (request.CancellationToken.IsCancellationRequested)
        {
            return;
        }

        ScrollBehavior? behavior = request.Behavior;
        if (behavior is null)
        {
            return;
        }

        ScrollTarget? target = await behavior(
            request.To,
            request.From,
            request.SavedPosition).ConfigureAwait(false);
        if (target is null || request.CancellationToken.IsCancellationRequested)
        {
            return;
        }

        interop.Scroll(target);
    }

    // The popstate handler: reconcile the arrived entry, honour a paused silent go, compute the
    // signed delta from the position counters, then notify listeners.
    private void OnPopState(BrowserHistorySnapshot snapshot)
    {
        var to = BrowserHistoryPathNormalization.CreateCurrentLocation(
            Base, snapshot.Pathname, snapshot.Search, snapshot.Hash);
        var from = currentLocation;
        var fromState = currentState;

        var delta = 0;
        if (snapshot.State is { } arrivedState)
        {
            currentLocation = to;
            currentState = arrivedState;
            if (pausedLocation is not null && string.Equals(pausedLocation, from, StringComparison.Ordinal))
            {
                // A silent go(delta, SuppressListeners) — state is reconciled, listeners are not.
                pausedLocation = null;
                return;
            }
            delta = arrivedState.Position - fromState.Position;
        }
        else
        {
            // An entry with no Viu state (created before this history, or a bare hash change):
            // synthesize one in place so subsequent navigations have a position to count from.
            Replace(to);
        }

        NotifyListeners(to, from, delta);
    }

    private void NotifyListeners(string to, string from, int delta)
    {
        var direction = delta > 0
            ? NavigationDirection.Forward
            : delta < 0
                ? NavigationDirection.Back
                : NavigationDirection.Unknown;
        var information = new NavigationInformation(NavigationType.Pop, direction, delta);
        // Snapshot so a listener that unsubscribes mid-notification does not disturb iteration.
        foreach (var callback in listeners.ToArray())
        {
            callback(to, from, information);
        }
    }

    // Compose the URL written to the environment: root-relative `base + location` for web mode, or
    // the `#…` fragment slice for hash mode.
    private string BuildUrl(string location)
    {
        var hashIndex = Base.IndexOf('#');
        return hashIndex >= 0 ? Base[hashIndex..] + location : Base + location;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(isDisposed, this);

    private readonly record struct PendingScrollRequest(
        RouteLocation To,
        RouteLocation From,
        ScrollPosition? SavedPosition,
        ScrollBehavior? Behavior,
        CancellationToken CancellationToken);
}
