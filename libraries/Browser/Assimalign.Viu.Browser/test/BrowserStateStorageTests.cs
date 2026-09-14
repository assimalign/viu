using System;

using Shouldly;
using Xunit;

using Assimalign.Viu.State;

namespace Assimalign.Viu.Browser.Tests;

// [STA-11]: construction is host-free; storage operations require the initialized Browser bridge.
public sealed class BrowserStateStorageTests
{
    [Fact]
    public void Constructor_DefaultKind_SelectsLocalWithoutAccessingTheBrowser()
    {
        new BrowserStateStorage().Kind.ShouldBe(StateStorageKind.Local);
    }

    [Theory]
    [InlineData(StateStorageKind.Local)]
    [InlineData(StateStorageKind.Session)]
    public void Constructor_ExplicitKind_PreservesSelectedStorageArea(StateStorageKind kind)
    {
        new BrowserStateStorage(kind).Kind.ShouldBe(kind);
    }

    [Fact]
    public void Constructor_UnknownKind_ThrowsBeforeAccessingTheBrowser()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => new BrowserStateStorage((StateStorageKind)99));
    }

    [Fact]
    public void Operations_NullArguments_ThrowBeforeAccessingTheBrowser()
    {
        BrowserStateStorage storage = new();

        Should.Throw<ArgumentNullException>(() => storage.TryRead(null!, out _));
        Should.Throw<ArgumentNullException>(() => storage.Write(null!, "value"));
        Should.Throw<ArgumentNullException>(() => storage.Write("key", null!));
        Should.Throw<ArgumentNullException>(() => storage.Remove(null!));
    }

    [Fact]
    public void Operations_UninitializedBridge_FailBeforeInterop()
    {
        BrowserStateStorage storage = new();

        Should.Throw<InvalidOperationException>(() => storage.TryRead("key", out _))
            .Message.ShouldContain("bridge is not initialized");
        Should.Throw<InvalidOperationException>(() => storage.Write("key", "value"))
            .Message.ShouldContain("bridge is not initialized");
        Should.Throw<InvalidOperationException>(() => storage.Remove("key"))
            .Message.ShouldContain("bridge is not initialized");
    }
}
