using System;

using Shouldly;
using Xunit;

namespace Assimalign.Vue.RuntimeDom.Tests;

// Pins the [V01.01.04.01] failure-semantics contract: JS-side failures surface as typed
// BrowserDomException instances carrying the operation name and handle, not opaque JSException
// strings. The translation itself is pure parsing, so it runs on the CoreCLR test host.
public class BrowserDomExceptionTests
{
    [Fact]
    public void Translate_ParsesStructuredBridgeErrors_IntoOperationAndHandle()
    {
#pragma warning disable CA1416 // Translate is platform-neutral parsing; only the imports are browser-only.
        var translated = BrowserDomBridge.Translate(
            "insert",
            5,
            new InvalidOperationException("vuecs-dom|remove|42|unknown DOM handle"));
#pragma warning restore CA1416

        // The JS-reported operation and handle win over the caller's.
        translated.OperationName.ShouldBe("remove");
        translated.NodeHandle.ShouldBe(42);
        translated.Message.ShouldContain("unknown DOM handle");
        translated.InnerException.ShouldBeOfType<System.InvalidOperationException>();
    }

    [Fact]
    public void Translate_WrapsUnstructuredFailures_WithTheCallingOperationAndHandle()
    {
#pragma warning disable CA1416
        var translated = BrowserDomBridge.Translate(
            "setAttribute",
            7,
            new InvalidOperationException("SyntaxError: The string did not match the expected pattern."));
#pragma warning restore CA1416

        translated.OperationName.ShouldBe("setAttribute");
        translated.NodeHandle.ShouldBe(7);
        translated.Message.ShouldContain("setAttribute");
        translated.Message.ShouldContain("7");
        translated.Message.ShouldContain("SyntaxError");
    }

    [Fact]
    public void Message_NamesTheOperationAndHandle()
    {
        var exception = new BrowserDomException("insert", 12, "anchor is not a child of the target parent");

        exception.Message.ShouldBe("DOM operation 'insert' failed for handle 12: anchor is not a child of the target parent");
        exception.OperationName.ShouldBe("insert");
        exception.NodeHandle.ShouldBe(12);
    }
}
