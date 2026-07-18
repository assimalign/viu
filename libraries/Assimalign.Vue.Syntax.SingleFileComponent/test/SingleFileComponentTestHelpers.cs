using System.Collections.Generic;

using Shouldly;

namespace Assimalign.Vue.Syntax.SingleFileComponent;

/// <summary>
/// Shared parsing helpers for the SingleFileComponent test corpus. All multi-line inputs are built from explicit
/// <c>\n</c> escapes (never literal control characters) so line endings are deterministic and visible.
/// </summary>
internal static class SingleFileComponentTestHelpers
{
    /// <summary>Parses <paramref name="source"/> and returns just the descriptor.</summary>
    public static SingleFileComponentDescriptor Parse(string source) => SingleFileComponentParser.Parse(source).Descriptor;

    /// <summary>Parses <paramref name="source"/> and returns the diagnostics.</summary>
    public static SyntaxList<SingleFileComponentError> Errors(string source) => SingleFileComponentParser.Parse(source).Errors;

    /// <summary>Enumerates every block in the descriptor: template, script, styles, then custom blocks.</summary>
    public static IEnumerable<SingleFileComponentBlock> AllBlocks(SingleFileComponentDescriptor descriptor)
    {
        if (descriptor.Template is not null)
        {
            yield return descriptor.Template;
        }

        if (descriptor.Script is not null)
        {
            yield return descriptor.Script;
        }

        foreach (var style in descriptor.Styles)
        {
            yield return style;
        }

        foreach (var custom in descriptor.CustomBlocks)
        {
            yield return custom;
        }
    }

    /// <summary>
    /// Asserts the [V01.01.06.01] span contract for every span the parser emits: for every block (whole
    /// and content region), every option, and every diagnostic, <c>Location.Source</c> equals the exact
    /// source slice between its offsets — and each block's <see cref="SingleFileComponentBlock.Content"/> equals its
    /// content-region slice.
    /// </summary>
    public static void AssertAllSpansExact(SingleFileComponentParseResult result)
    {
        var source = result.Descriptor.Source;
        foreach (var block in AllBlocks(result.Descriptor))
        {
            AssertSpan(block.Location, source);
            AssertSpan(block.ContentLocation, source);
            block.Content.ShouldBe(block.ContentLocation.Source);
            foreach (var option in block.Options)
            {
                AssertSpan(option.Location, source);
            }
        }

        foreach (var error in result.Errors)
        {
            AssertSpan(error.Location, source);
        }
    }

    private static void AssertSpan(SourceLocation location, string source)
    {
        var length = location.End.Offset - location.Start.Offset;
        location.Source.ShouldBe(source.Substring(location.Start.Offset, length));
    }
}
