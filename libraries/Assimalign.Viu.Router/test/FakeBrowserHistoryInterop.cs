using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Router.Tests;

// An instrumented stand-in for the browser edge (the same seam the real
// JavaScriptBrowserHistoryInterop implements): it records every crossing and lets a test drive a
// popstate, so the whole BrowserRouterHistory policy — base handling, state round-trip, listener
// bookkeeping, and interop-call counting — is exercised with no browser. Mirrors the recorded-bridge
// approach of RuntimeDom's BrowserEventInvokerRegistryTests.
internal sealed class FakeBrowserHistoryInterop : IBrowserHistoryInterop
{
    private Action<BrowserHistorySnapshot>? popStateHandler;

    internal FakeBrowserHistoryInterop(BrowserHistorySnapshot initialSnapshot)
        => InitialSnapshot = initialSnapshot;

    // The snapshot returned by ReadSnapshot (the environment the policy bootstraps from).
    internal BrowserHistorySnapshot InitialSnapshot { get; set; }

    internal string? BaseHref { get; set; }

    internal int ReadSnapshotCount { get; private set; }

    internal int SubscribeCount { get; private set; }

    internal int UnsubscribeCount { get; private set; }

    internal bool IsSubscribed => popStateHandler is not null;

    internal List<(string CurrentUrl, RouterHistoryState AmendedCurrent, string ToUrl, RouterHistoryState NewState)> PushCalls { get; } = [];

    internal List<(string ToUrl, RouterHistoryState NewState)> ReplaceCalls { get; } = [];

    internal List<int> GoCalls { get; } = [];

    public BrowserHistorySnapshot ReadSnapshot()
    {
        ReadSnapshotCount++;
        return InitialSnapshot;
    }

    public string? ReadBaseHref()
        => BaseHref;

    public void Push(string currentUrl, RouterHistoryState amendedCurrentState, string toUrl, RouterHistoryState newState)
        => PushCalls.Add((currentUrl, amendedCurrentState, toUrl, newState));

    public void Replace(string toUrl, RouterHistoryState newState)
        => ReplaceCalls.Add((toUrl, newState));

    public void Go(int delta)
        => GoCalls.Add(delta);

    public void Subscribe(Action<BrowserHistorySnapshot> onPopState)
    {
        popStateHandler = onPopState;
        SubscribeCount++;
    }

    public void Unsubscribe()
    {
        popStateHandler = null;
        UnsubscribeCount++;
    }

    // Simulate the browser firing popstate for an arrived entry (what the JS listener would forward
    // through the [JSExport] dispatch).
    internal void FirePopState(BrowserHistorySnapshot arrived)
        => popStateHandler?.Invoke(arrived);
}
