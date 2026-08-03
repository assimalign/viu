using System;
using System.Collections.Generic;
using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// Completeness and message pins for the compiler error catalog ([V01.01.05.08]): every
/// <see cref="CompilerErrorCode"/> that is a real code (not one of the two reserved extend-point
/// sentinels) must have a human-readable message, and the spelled-out messages below ARE the contract —
/// they are observable build output that consumers grep and CI logs quote, so a wording change is a
/// deliberate change, never cleanup. A future code added without a message fails
/// <see cref="EveryErrorCode_HasANonEmptyMessage"/>, so the diagnostic surface can never regress into
/// reporting a blank message.
/// </summary>
public sealed class CompilerErrorCatalogTests
{
    // The two reserved sentinels, each one past the last core / DOM code. They carry
    // no message because they never name a diagnostic — they only anchor the numeric extension band.
    private static readonly HashSet<CompilerErrorCode> Sentinels = new()
    {
        CompilerErrorCode.ExtendPoint,
        CompilerErrorCode.DomExtendPoint,
    };

    public static IEnumerable<object[]> RealCodes()
        => Enum.GetValues<CompilerErrorCode>()
            .Where(code => !Sentinels.Contains(code))
            .Select(code => new object[] { code });

    [Theory]
    [MemberData(nameof(RealCodes))]
    public void EveryErrorCode_HasANonEmptyMessage(CompilerErrorCode code)
    {
        // The catalog-completeness audit: every non-sentinel code resolves to a non-empty message so a
        // reported diagnostic is never blank. Two codes (X_INVALID_EXPRESSION, the Viu unresolved-
        // identifier code) are message PREFIXES the reporter appends detail to; both are non-empty.
        CompilerErrorMessages.GetMessage(code).ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Sentinels_HaveNoMessage_ByDesign()
    {
        // The reserved extension sentinels are not diagnostics; they intentionally have no message.
        CompilerErrorMessages.GetMessage(CompilerErrorCode.ExtendPoint).ShouldBeEmpty();
        CompilerErrorMessages.GetMessage(CompilerErrorCode.DomExtendPoint).ShouldBeEmpty();
    }

    [Theory]
    // The messages the issue calls out by name, plus a representative spread across the parse,
    // core-transform, and DOM-transform bands.
    [InlineData(CompilerErrorCode.XVIfNoExpression, "v-if/v-else-if is missing expression.")]
    [InlineData(CompilerErrorCode.XVForMalformedExpression, "v-for has invalid expression.")]
    [InlineData(CompilerErrorCode.XVSlotMisplaced, "v-slot can only be used on components or <template> tags.")]
    [InlineData(CompilerErrorCode.XVElseNoAdjacentIf, "v-else/v-else-if has no adjacent v-if or v-else-if.")]
    [InlineData(CompilerErrorCode.XVHtmlWithChildren, "v-html will override element children.")]
    [InlineData(CompilerErrorCode.XVShowNoExpression, "v-show is missing expression.")]
    [InlineData(CompilerErrorCode.XMissingInterpolationEnd, "Interpolation end sign was not found.")]
    [InlineData(CompilerErrorCode.XInvalidExpression, "Error parsing JavaScript expression: ")]
    public void Message_MatchesUpstreamText(CompilerErrorCode code, string expected)
        => CompilerErrorMessages.GetMessage(code).ShouldBe(expected);
}
