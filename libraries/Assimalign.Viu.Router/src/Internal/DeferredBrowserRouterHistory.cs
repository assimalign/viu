using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.Router;

/// <summary>
/// Defers construction of the synchronous browser-history policy until the router's asynchronous
/// readiness path has loaded the JavaScript bridge.
/// </summary>
internal sealed class DeferredBrowserRouterHistory :
    IRouterHistory,
    IInitializableRouterHistory
{
    internal const string NotReadyMessage =
        "Browser history is not ready. Await Router.ReadyAsync() before accessing synchronous history members.";

    private readonly bool _isHash;
    private readonly string? _basePath;
    private readonly Func<CancellationToken, Task> _initializeBridge;
    private readonly Func<IBrowserHistoryInterop> _createBrowserHistoryInterop;
    private readonly List<NavigationCallback> _listeners = [];
    private BrowserRouterHistory? _history;
    private Task? _initialization;
    private bool _isDisposed;

    internal DeferredBrowserRouterHistory(
        bool isHash,
        string? basePath,
        Func<CancellationToken, Task> initializeBridge,
        Func<IBrowserHistoryInterop> createBrowserHistoryInterop)
    {
        ArgumentNullException.ThrowIfNull(initializeBridge);
        ArgumentNullException.ThrowIfNull(createBrowserHistoryInterop);
        _isHash = isHash;
        _basePath = basePath;
        _initializeBridge = initializeBridge;
        _createBrowserHistoryInterop = createBrowserHistoryInterop;
    }

    /// <inheritdoc/>
    public string Base => GetHistory().Base;

    /// <inheritdoc/>
    public string Location => GetHistory().Location;

    /// <inheritdoc/>
    public RouterHistoryState State => GetHistory().State;

    /// <inheritdoc/>
    public void Push(string location, RouterHistoryEntryOptions options = default)
        => GetHistory().Push(location, options);

    /// <inheritdoc/>
    public void Replace(string location, RouterHistoryEntryOptions options = default)
        => GetHistory().Replace(location, options);

    /// <inheritdoc/>
    public void Go(
        int delta,
        RouterHistoryNavigationOptions options = RouterHistoryNavigationOptions.None)
        => GetHistory().Go(delta, options);

    /// <inheritdoc/>
    public Action Listen(NavigationCallback callback)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(callback);
        _listeners.Add(callback);
        bool isRemoved = false;
        return () =>
        {
            if (isRemoved)
            {
                return;
            }

            isRemoved = true;
            _listeners.Remove(callback);
        };
    }

    /// <inheritdoc/>
    public string CreateHref(string location)
        => GetHistory().CreateHref(location);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _listeners.Clear();
        _history?.Dispose();
    }

    /// <inheritdoc/>
    public ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        return new ValueTask(
            _initialization ??= InitializeCoreAsync(cancellationToken));
    }

    private async Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        await _initializeBridge(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        IBrowserHistoryInterop browserHistoryInterop =
            _createBrowserHistoryInterop();
        string normalizedBase = _isHash
            ? RouterHistory.ResolveHashBase(browserHistoryInterop, _basePath)
            : RouterHistory.ResolveWebBase(browserHistoryInterop, _basePath);
        BrowserRouterHistory history =
            new(browserHistoryInterop, normalizedBase);
        history.Listen(NotifyListeners);
        _history = history;
    }

    private BrowserRouterHistory GetHistory()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        return _history
            ?? throw new InvalidOperationException(NotReadyMessage);
    }

    private void NotifyListeners(
        string to,
        string from,
        NavigationInformation information)
    {
        foreach (NavigationCallback listener in _listeners.ToArray())
        {
            listener(to, from, information);
        }
    }
}
