using System;

using Shouldly;
using Xunit;

using Assimalign.Viu.Browser;

namespace Assimalign.Viu.Browser.Tests;

public sealed class BrowserVirtualNodeHostTests
{
    [Fact]
    public void CreateText_DesignScaffold_LeavesInteropAsHostImplementationWork()
    {
        var host = new BrowserVirtualNodeHost();

        Should.Throw<NotSupportedException>(() => host.CreateText("value"));
    }
}
