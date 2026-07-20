using System;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Router.Tests;

// Pins the flat, primitives-only wire format the single batched read returns from the JS edge — the
// state round-trip (position + scroll anchor) the [V01.01.08.02] criterion requires, verified without
// a browser by decoding a hand-built payload.
public class BrowserHistorySnapshotMarshallerTests
{
    [Fact]
    public void Decode_FullStateWithScroll_RoundTripsEveryField()
    {
        var raw = new[]
        {
            "/app/users", "?q=1", "#section", "example.com", "10",
            "1",                 // hasState
            "/app",              // back
            "/app/users",        // current
            "",                  // forward (empty -> null)
            "1",                 // replaced
            "7",                 // position
            "1",                 // hasScroll
            "12.5", "34",        // scroll left/top
        };

        var snapshot = BrowserHistorySnapshotMarshaller.Decode(raw);

        snapshot.Pathname.ShouldBe("/app/users");
        snapshot.Search.ShouldBe("?q=1");
        snapshot.Hash.ShouldBe("#section");
        snapshot.Host.ShouldBe("example.com");
        snapshot.HistoryLength.ShouldBe(10);

        var state = snapshot.State.ShouldNotBeNull();
        state.Back.ShouldBe("/app");
        state.Current.ShouldBe("/app/users");
        state.Forward.ShouldBeNull();          // "" decodes to null
        state.Replaced.ShouldBeTrue();
        state.Position.ShouldBe(7);
        state.Scroll.ShouldBe(new ScrollPosition(12.5, 34));
    }

    [Fact]
    public void Decode_StateWithoutScroll_LeavesScrollNullAndKeepsForwardLink()
    {
        var raw = new[]
        {
            "/", "", "", "example.com", "3",
            "1", "", "/", "/next", "0", "2", "0", "0", "0",
        };

        var state = BrowserHistorySnapshotMarshaller.Decode(raw).State.ShouldNotBeNull();

        state.Back.ShouldBeNull();             // "" -> null
        state.Forward.ShouldBe("/next");
        state.Replaced.ShouldBeFalse();
        state.Position.ShouldBe(2);
        state.Scroll.ShouldBeNull();
    }

    [Fact]
    public void Decode_NoState_YieldsNullState()
    {
        var raw = new[]
        {
            "/", "", "", "example.com", "1",
            "0", "", "", "", "0", "0", "0", "0", "0",
        };

        BrowserHistorySnapshotMarshaller.Decode(raw).State.ShouldBeNull();
    }

    [Fact]
    public void Decode_WrongLength_Throws()
        => Should.Throw<ArgumentException>(() => BrowserHistorySnapshotMarshaller.Decode(["too", "short"]));
}
