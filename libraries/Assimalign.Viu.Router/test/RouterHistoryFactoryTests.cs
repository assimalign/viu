using Shouldly;
using Xunit;

namespace Assimalign.Viu.Router.Tests;

// Pins the factory base-resolution helpers of RouterHistory — the C# port of how createWebHistory
// defaults its base from <base href> and how createWebHashHistory computes a hash base
// (packages/router/src/history/html5.ts, hash.ts) — plus the memory mode's no-browser guarantee.
public class RouterHistoryFactoryTests
{
    private static FakeBrowserHistoryInterop Interop(
        string host = "example.com",
        string pathname = "/",
        string search = "")
        => new(new BrowserHistorySnapshot(pathname, search, string.Empty, host, 1, null));

    [Fact]
    public void ResolveWebBase_ConfiguredBase_Normalized()
    {
        var resolved = RouterHistory.ResolveWebBase(Interop(), "/app/");

        resolved.ShouldBe("/app");
    }

    [Fact]
    public void ResolveWebBase_NoBase_UsesBaseHrefWithOriginStripped()
    {
        var interop = Interop();
        interop.BaseHref = "https://example.com/base/";

        var resolved = RouterHistory.ResolveWebBase(interop, basePath: null);

        resolved.ShouldBe("/base");
    }

    [Fact]
    public void ResolveWebBase_NoBaseAndNoBaseElement_DefaultsToEmpty()
    {
        var interop = Interop();
        interop.BaseHref = null;

        var resolved = RouterHistory.ResolveWebBase(interop, basePath: null);

        resolved.ShouldBe("");   // "/" default -> trailing slash trimmed
    }

    [Fact]
    public void ResolveHashBase_NoBase_DerivesFromLocationWithHash()
    {
        var resolved = RouterHistory.ResolveHashBase(Interop(pathname: "/folder/"), basePath: null);

        resolved.ShouldBe("/folder/#");
    }

    [Fact]
    public void ResolveHashBase_ConfiguredBase_GetsAHash()
    {
        var resolved = RouterHistory.ResolveHashBase(Interop(), "/app/");

        resolved.ShouldBe("/app/#");
    }

    [Fact]
    public void ResolveHashBase_HostlessFileUrl_YieldsBareHash()
    {
        var resolved = RouterHistory.ResolveHashBase(Interop(host: string.Empty, pathname: "/folder/"), basePath: null);

        resolved.ShouldBe("#");
    }

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
