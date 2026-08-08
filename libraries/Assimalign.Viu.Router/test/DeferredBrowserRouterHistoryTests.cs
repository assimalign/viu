using System;
using System.Runtime.Versioning;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Router.Tests;

// Pins the public lazy browser-history contract without reaching through a production friend grant:
// creation and listener registration are safe before readiness, while synchronous state access
// directs the caller to Router.ReadyAsync. Specified by [RTR-3].
public sealed class DeferredBrowserRouterHistoryTests
{
    [Fact]
    public void InitializedBrowserHistory_DisposeUnsubscribesAndIsTerminal()
    {
        var interop = new RecordingBrowserHistoryInterop();
        var history = new BrowserRouterHistory(interop, string.Empty);

        history.Dispose();
        history.Dispose();

        interop.UnsubscribeCount.ShouldBe(1);
        Should.Throw<ObjectDisposedException>(() => _ = history.Base);
        Should.Throw<ObjectDisposedException>(() => _ = history.Location);
        Should.Throw<ObjectDisposedException>(() => _ = history.State);
        Should.Throw<ObjectDisposedException>(() => history.Push("/next"));
        Should.Throw<ObjectDisposedException>(() => history.Replace("/next"));
        Should.Throw<ObjectDisposedException>(() => history.Go(-1));
        Should.Throw<ObjectDisposedException>(
            () => history.Listen(static (_, _, _) => { }));
        Should.Throw<ObjectDisposedException>(() => history.CreateHref("/next"));
    }

    [Fact]
    [SupportedOSPlatform("browser")]
    public void CreateWebAndHash_BeforeReady_DeferSynchronousStateAccess()
    {
        IRouterHistory[] histories =
        [
            RouterHistory.CreateWeb(),
            RouterHistory.CreateWebHash(),
        ];

        foreach (IRouterHistory history in histories)
        {
            Action stopListening = history.Listen(static (_, _, _) => { });
            InvalidOperationException exception =
                Should.Throw<InvalidOperationException>(() => _ = history.Location);

            exception.Message.ShouldContain(nameof(Router.ReadyAsync));

            stopListening();
            stopListening();
            history.Dispose();
            Should.Throw<ObjectDisposedException>(() => _ = history.Location);
            Should.NotThrow(history.Dispose);
        }
    }

    private sealed class RecordingBrowserHistoryInterop : IBrowserHistoryInterop
    {
        internal int UnsubscribeCount { get; private set; }

        public BrowserHistorySnapshot ReadSnapshot() =>
            new("/", string.Empty, string.Empty, "example.test", 1, null);

        public string? ReadBaseHref() => null;

        public void Push(
            string currentUrl,
            RouterHistoryState amendedCurrentState,
            string toUrl,
            RouterHistoryState newState)
        {
        }

        public void Replace(string toUrl, RouterHistoryState newState)
        {
        }

        public void Go(int delta)
        {
        }

        public void Subscribe(Action<BrowserHistorySnapshot> onPopState)
        {
        }

        public void Unsubscribe() => UnsubscribeCount++;
    }
}
