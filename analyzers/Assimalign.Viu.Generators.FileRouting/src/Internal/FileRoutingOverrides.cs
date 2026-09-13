using System;
using System.Collections.Generic;
using System.Text;

using Assimalign.Viu.Syntax;
using Assimalign.Viu.Syntax.SingleFileComponent;

namespace Assimalign.Viu.Generators.FileRouting;

internal static class FileRoutingOverrides
{
    internal static bool TryRead(FileRoutingPage page, out string? problem)
    {
        SyntaxList<SingleFileComponentCustomBlock> blocks;
        SyntaxList<SingleFileComponentError> errors;
        if (page.Input.Path.EndsWith(".vue", StringComparison.OrdinalIgnoreCase))
        {
            var parsed = VueSingleFileComponentParser.Parse(page.Input.Text);
            blocks = parsed.Descriptor.CustomBlocks;
            errors = parsed.Errors;
        }
        else
        {
            var parsed = SingleFileComponentParser.Parse(page.Input.Text);
            blocks = parsed.Descriptor.CustomBlocks;
            errors = parsed.Errors;
        }

        SingleFileComponentCustomBlock? route = null;
        foreach (var block in blocks)
        {
            if (block.Name != "route")
            {
                continue;
            }
            if (route is not null)
            {
                problem = "only one route block is allowed";
                return false;
            }
            route = block;
        }
        if (route is null)
        {
            // Malformed container openers may never become custom blocks. Reuse the parser's error
            // location and source rather than interpreting template/script contents as route blocks.
            foreach (var error in errors)
            {
                if (error.Location.Source.StartsWith("@route", StringComparison.Ordinal)
                    || error.Location.Source.StartsWith("<route", StringComparison.Ordinal))
                {
                    problem = "the route container is incomplete";
                    return false;
                }
            }
            problem = null;
            return true;
        }

        if (route.Options.Count != 0)
        {
            problem = "route block options are not supported";
            return false;
        }
        foreach (var error in errors)
        {
            if (error.Severity == DiagnosticSeverity.Error
                && error.Location.Start.Offset >= route.Location.Start.Offset
                && error.Location.Start.Offset <= route.Location.End.Offset)
            {
                problem = "the route container is incomplete";
                return false;
            }
        }

        var assignments = new Dictionary<string, string>(StringComparer.Ordinal);
        var content = route.Content;
        var position = 0;
        while (true)
        {
            SkipWhitespace(content, ref position);
            if (position == content.Length)
            {
                break;
            }
            var start = position;
            while (position < content.Length && content[position] is >= 'a' and <= 'z')
            {
                position++;
            }
            var key = content.Substring(start, position - start);
            if (key is not ("path" or "name") || assignments.ContainsKey(key)
                || !Consume(content, ref position, '=') || !Consume(content, ref position, '"')
                || !TryString(content, ref position, out var value) || !Consume(content, ref position, ';'))
            {
                problem = "expected unique path or name assignments of the form path = \"/example\"; (only quote and backslash escapes are allowed)";
                return false;
            }
            assignments.Add(key, value);
        }

        assignments.TryGetValue("path", out var path);
        assignments.TryGetValue("name", out var name);
        if (name is not null && (string.IsNullOrWhiteSpace(name) || ContainsControl(name)))
        {
            problem = "name must be nonempty and contain no control characters";
            return false;
        }
        page.PathOverride = path;
        page.NameOverride = name;
        problem = null;
        return true;
    }

    private static bool TryString(string content, ref int position, out string value)
    {
        var builder = new StringBuilder();
        while (position < content.Length)
        {
            var character = content[position++];
            if (character == '"')
            {
                value = builder.ToString();
                return true;
            }
            if (character == '\\')
            {
                if (position == content.Length || content[position] is not ('\\' or '"'))
                {
                    break;
                }
                character = content[position++];
            }
            if (char.IsControl(character))
            {
                break;
            }
            builder.Append(character);
        }
        value = string.Empty;
        return false;
    }

    private static bool Consume(string content, ref int position, char expected)
    {
        SkipWhitespace(content, ref position);
        if (position == content.Length || content[position] != expected)
        {
            return false;
        }
        position++;
        return true;
    }

    private static void SkipWhitespace(string content, ref int position)
    {
        while (position < content.Length && char.IsWhiteSpace(content[position]))
        {
            position++;
        }
    }

    private static bool ContainsControl(string value)
    {
        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                return true;
            }
        }
        return false;
    }
}
