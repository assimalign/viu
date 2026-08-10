using System.Linq;
using System.Runtime.CompilerServices;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Router.Tests;

// Pins the D8 assembly boundary: production internals may be exposed only to this assembly's own
// test project. Downstream histories integrate through the public [RTR-7] capability interfaces.
public sealed class RouterAssemblyBoundaryTests
{
    [Fact]
    public void RouterAssembly_GrantsInternalAccessOnlyToItsOwnTests()
    {
        string[] grants = typeof(Router)
            .Assembly
            .GetCustomAttributes(typeof(InternalsVisibleToAttribute), inherit: false)
            .Cast<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName)
            .Order()
            .ToArray();

        grants.ShouldBe(["Assimalign.Viu.Router.Tests"]);
    }
}
