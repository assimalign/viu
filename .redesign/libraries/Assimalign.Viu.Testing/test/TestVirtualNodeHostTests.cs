using Assimalign.Viu.Components;
using Assimalign.Viu.Testing;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Testing.Tests;

public sealed class TestVirtualNodeHostTests
{
    [Fact]
    public void Insert_ChildBeforeAnchor_PreservesHostTopology()
    {
        var host = new TestVirtualNodeHost();
        var parent = host.CreateElement(new QualifiedName("parent"));
        var last = host.CreateText("last");
        var first = host.CreateText("first");
        host.Insert(last, parent, null);

        host.Insert(first, parent, last);

        host.GetParent(first).ShouldBeSameAs(parent);
        host.GetNextSibling(first).ShouldBeSameAs(last);
    }
}
