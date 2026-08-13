using Shouldly;
using Xunit;

namespace Assimalign.Viu.Router.Tests;

// Pins the public memory-history factory contract on a plain .NET host. Browser base normalization
// remains an implementation detail of the platform-gated history edge. Specified by [RTR-3].
public class RouterHistoryFactoryTests
{
    [Fact]
    public void CreateMemory_NeedsNoInitializationAndIsInteropFree()
    {
        // The memory mode works with no InitializeAsync and no browser at all — the proof it carries
        // zero interop dependency (it is exercised here on a plain .NET host).
        var history = RouterHistory.CreateMemory();

        history.Push("/a");
        history.Location.ShouldBe("/a");
    }
}
