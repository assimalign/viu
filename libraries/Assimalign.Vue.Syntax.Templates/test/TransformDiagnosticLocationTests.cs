using System.Collections.Generic;
using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// Location-precision pins for transform-stage diagnostics ([V01.01.05.08]): every <c>v-if</c>/<c>v-for</c>/
/// <c>v-slot</c>/<c>v-on</c>/<c>v-bind</c>/<c>v-model</c>/<c>v-html</c>/<c>v-show</c> misuse must report a
/// diagnostic whose <see cref="SourceLocation"/> points at the exact offending span (the directive, its
/// argument, its expression, or the element), so the source-mapped Roslyn diagnostic lands a squiggle on the
/// right template characters rather than on the whole element or a zero-width guess. The precise span each
/// transform passes is the upstream contract (<c>@vue/compiler-core</c> / <c>@vue/compiler-dom</c>
/// <c>transforms/*</c>); this pins that <c>Location.Source</c> equals the exact slice and the start position
/// is correct.
/// </summary>
public sealed class TransformDiagnosticLocationTests
{
    public static IEnumerable<object[]> Cases()
    {
        // source, code, exact Location.Source slice, start line, start column
        yield return C("<div v-if></div>", CompilerErrorCode.XVIfNoExpression, "v-if", 1, 6);
        yield return C("<div v-for=\"bad\"></div>", CompilerErrorCode.XVForMalformedExpression, "v-for=\"bad\"", 1, 6);
        yield return C("<span>x</span><div v-else></div>", CompilerErrorCode.XVElseNoAdjacentIf, "<div v-else></div>", 1, 15);
        yield return C("<div @click></div>", CompilerErrorCode.XVOnNoExpression, "@click", 1, 6);
        yield return C("<div v-slot=\"x\">y</div>", CompilerErrorCode.XVSlotMisplaced, "v-slot=\"x\"", 1, 6);
        yield return C("<input v-model />", CompilerErrorCode.XVModelNoExpression, "v-model", 1, 8);
        yield return C("<div v-html></div>", CompilerErrorCode.XVHtmlNoExpression, "v-html", 1, 6);
        yield return C("<div v-html=\"h\">child</div>", CompilerErrorCode.XVHtmlWithChildren, "v-html=\"h\"", 1, 6);
        yield return C("<div v-show></div>", CompilerErrorCode.XVShowNoExpression, "v-show", 1, 6);
        // v-bind same-name shorthand with a dynamic argument: the error points at the dynamic argument span.
        yield return C("<div :[dynamicName]></div>", CompilerErrorCode.XVBindInvalidSameNameArgument, "[dynamicName]", 1, 7);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TransformError_PointsAtTheExactOffendingSpan(
        string source,
        CompilerErrorCode code,
        string expectedSource,
        int startLine,
        int startColumn)
    {
        TransformTestHelpers.Transform(source, out var errors);

        var error = errors.FirstOrDefault(candidate => candidate.Code == code)
            .ShouldNotBeNull($"expected a {code} diagnostic for: {source}");
        error.Location.Source.ShouldBe(expectedSource);
        error.Location.Start.Line.ShouldBe(startLine);
        error.Location.Start.Column.ShouldBe(startColumn);
        // The slice is self-consistent: Location.Source is exactly the characters between the offsets.
        source.Substring(error.Location.Start.Offset, error.Location.End.Offset - error.Location.Start.Offset)
            .ShouldBe(expectedSource);
    }

    private static object[] C(string source, CompilerErrorCode code, string expectedSource, int line, int column)
        => new object[] { source, code, expectedSource, line, column };
}
