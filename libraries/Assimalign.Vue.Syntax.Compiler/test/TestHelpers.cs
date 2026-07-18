using System.Collections.Generic;

using Shouldly;

namespace Assimalign.Vue.Syntax.Compiler;

/// <summary>
/// Shared parsing helpers for the test corpus. Mirrors the setup in vuejs/core's
/// <c>packages/compiler-core/__tests__/parse.spec.ts</c>: parse in base mode, and capture the
/// recoverable errors reported through <see cref="ParserOptions.OnError"/>.
/// </summary>
internal static class TestHelpers
{
    /// <summary>Parses <paramref name="source"/> in base mode, ignoring errors.</summary>
    public static RootNode Parse(string source) => TemplateParser.Parse(source);

    /// <summary>Parses <paramref name="source"/>, collecting any reported errors into <paramref name="errors"/>.</summary>
    public static RootNode Parse(string source, out List<CompilerError> errors, ParserOptions? options = null)
    {
        var collected = new List<CompilerError>();
        options ??= new ParserOptions();
        options.OnError = collected.Add;
        var root = TemplateParser.Parse(source, options);
        errors = collected;
        return root;
    }

    /// <summary>Returns the errors reported while parsing <paramref name="source"/>.</summary>
    public static List<CompilerError> Errors(string source, ParserOptions? options = null)
    {
        Parse(source, out var errors, options);
        return errors;
    }

    /// <summary>
    /// Slices <paramref name="source"/> the way the parser does (clamping out-of-range or inverted
    /// ranges to the empty string), so the location contract holds for pathological nodes too.
    /// </summary>
    public static string Slice(string source, int start, int end)
    {
        if (start < 0)
        {
            start = 0;
        }

        if (end > source.Length)
        {
            end = source.Length;
        }

        if (end < start)
        {
            end = start;
        }

        return source.Substring(start, end - start);
    }

    /// <summary>
    /// Asserts the [V01.01.05.01] location contract for every node reachable from <paramref name="root"/>:
    /// each node's <c>Location.Source</c> equals the exact source slice between its offsets.
    /// </summary>
    public static void AssertAllLocationsExact(RootNode root)
    {
        Visit(root, root.Source);
    }

    private static void Visit(TemplateSyntaxNode node, string source)
    {
        AssertLocation(node.Location, source);
        switch (node)
        {
            case RootNode root:
                foreach (var child in root.Children)
                {
                    Visit(child, source);
                }

                break;
            case ElementNode element:
                foreach (var property in element.Properties)
                {
                    Visit(property, source);
                }

                foreach (var child in element.Children)
                {
                    Visit(child, source);
                }

                break;
            case AttributeNode attribute:
                AssertLocation(attribute.NameLocation, source);
                if (attribute.Value is not null)
                {
                    Visit(attribute.Value, source);
                }

                break;
            case DirectiveNode directive:
                if (directive.Argument is not null)
                {
                    Visit(directive.Argument, source);
                }

                foreach (var modifier in directive.Modifiers)
                {
                    Visit(modifier, source);
                }

                if (directive.Expression is not null)
                {
                    Visit(directive.Expression, source);
                }

                break;
            case InterpolationNode interpolation:
                Visit(interpolation.Content, source);
                break;
        }
    }

    private static void AssertLocation(SourceLocation location, string source)
        => location.Source.ShouldBe(Slice(source, location.Start.Offset, location.End.Offset));
}
