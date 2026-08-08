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
            history.Destroy();
        }
    }
}
