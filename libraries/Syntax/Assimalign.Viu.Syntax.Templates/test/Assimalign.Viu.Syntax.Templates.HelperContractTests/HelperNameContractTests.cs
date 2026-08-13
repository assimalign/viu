using System;
using System.Linq;

using Shouldly;
using Xunit;

using Assimalign.Viu.Syntax.Templates;

namespace Assimalign.Viu.Syntax.Templates.HelperContractTests;

// [V01.01.15.02] Browser operation identities remain a stable, duplicate-free subset of the symbolic
// compiler table. The render writer lowers them to typed operations rather than runtime member lookup.
public sealed class HelperNameContractTests
{
    [Fact]
    public void DomHelpers_RegisteredIdentities_AreUniqueAndUseCanonicalNames()
    {
        RuntimeHelper[] helpers = HelperNames.DomHelpers;

        helpers.ShouldNotBeEmpty();
        helpers.Select(helper => helper.Name).Distinct(StringComparer.Ordinal).Count()
            .ShouldBe(helpers.Length);
        helpers.ShouldAllBe(helper => !string.IsNullOrWhiteSpace(helper.Name));
        helpers.ShouldAllBe(helper => !helper.Name.StartsWith("_", StringComparison.Ordinal));
    }
}
