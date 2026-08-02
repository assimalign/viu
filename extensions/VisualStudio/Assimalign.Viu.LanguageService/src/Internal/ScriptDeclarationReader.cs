using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// Reads the members a component's <c>@script</c> block declares, using a syntax-only Roslyn parse
/// (no compilation, no workspace) — the completion-facing analogue of <c>@vue/compiler-sfc</c>'s
/// <c>compileScript()</c> binding scan ([V01.01.12.07.04], #261). Results are cached keyed on the
/// script-block text, so an edit inside a template or style block never pays a reparse.
/// </summary>
/// <remarks>
/// Not thread-safe: every call site runs inside the owning service's request lock, so the reader
/// assumes single-threaded use and takes no internal lock.
/// </remarks>
internal sealed class ScriptDeclarationReader
{
    // Mirrors ScriptBlockAnalyzer's region split and probe wrapper
    // (analyzers\Assimalign.Viu.Generators.Syntax\src\Internal\ScriptBlockAnalyzer.cs); keep the
    // split rules in sync.
    private const string ProbePrefix = "partial class __ViuScriptProbe\n{\n";
    private const string ProbeSuffix = "\n}\n";
    private const string ProbeClassName = "__ViuScriptProbe";

    // A document has at most two script blocks (@script + <script setup>), so this bound
    // comfortably covers interleaved requests across a few open documents while making growth
    // impossible.
    private const int MaximumCacheEntries = 8;

    // DocumentationMode.Parse — deliberately different from the analyzer's None — because this
    // reader wants the /// summary text as structured trivia.
    private static readonly CSharpParseOptions ParseOptions =
        new(LanguageVersion.Preview, DocumentationMode.Parse, SourceCodeKind.Regular);

    private readonly Dictionary<string, IReadOnlyList<ScriptDeclaredMember>> cache =
        new(StringComparer.Ordinal);

    /// <summary>Gets how many <see cref="Read"/> calls missed the cache and parsed.</summary>
    internal int CacheMissCount { get; private set; }

    /// <summary>Reads the members declared by <paramref name="scriptContent"/>.</summary>
    /// <param name="scriptContent">The raw <c>@script</c> block content.</param>
    /// <returns>The declared fields, properties, and methods, in declaration order.</returns>
    internal IReadOnlyList<ScriptDeclaredMember> Read(string scriptContent)
    {
        if (cache.TryGetValue(scriptContent, out var cached))
        {
            return cached;
        }

        CacheMissCount++;
        var members = Extract(scriptContent);
        if (cache.Count == MaximumCacheEntries)
        {
            cache.Clear();
        }

        cache[scriptContent] = members;
        return members;
    }

    private static IReadOnlyList<ScriptDeclaredMember> Extract(string scriptContent)
    {
        // Leading-using split, mirroring ScriptBlockAnalyzer.Analyze: hoisted usings are illegal
        // inside the probe class and would degrade recovery, so only the member region is probed.
        // C# forbids members before usings, so CompilationUnitSyntax.Usings is exactly the leading
        // run; the cut is at the start of the first line after the last using.
        var splitTree = CSharpSyntaxTree.ParseText(scriptContent, ParseOptions);
        var leadingUsings = ((CompilationUnitSyntax)splitTree.GetRoot()).Usings;
        var memberRegion = scriptContent;
        if (leadingUsings.Count > 0)
        {
            var lastUsing = leadingUsings[leadingUsings.Count - 1];
            var lastUsingEndLine = lastUsing.GetLocation().GetLineSpan().EndLinePosition.Line;
            var memberLineIndex = lastUsingEndLine + 1;
            var lines = splitTree.GetText().Lines;
            var splitOffset = memberLineIndex < lines.Count
                ? lines[memberLineIndex].Start
                : scriptContent.Length;
            memberRegion = scriptContent.Substring(splitOffset);
        }

        if (string.IsNullOrWhiteSpace(memberRegion))
        {
            return Array.Empty<ScriptDeclaredMember>();
        }

        // Parse diagnostics are ignored: Roslyn error recovery still yields the well-formed
        // members, and diagnostics remain the generator's job.
        var tree = CSharpSyntaxTree.ParseText(
            ProbePrefix + memberRegion + ProbeSuffix,
            ParseOptions);
        var probe = FindProbe((CompilationUnitSyntax)tree.GetRoot());
        if (probe is null)
        {
            return Array.Empty<ScriptDeclaredMember>();
        }

        var members = new List<ScriptDeclaredMember>();
        foreach (var member in probe.Members)
        {
            AppendMember(member, members);
        }

        return members.Count == 0
            ? Array.Empty<ScriptDeclaredMember>()
            : members;
    }

    // The probe is normally the sole top-level member; a search by name is used so malformed
    // content that injects stray top-level declarations still finds the real members.
    private static ClassDeclarationSyntax? FindProbe(CompilationUnitSyntax root)
    {
        foreach (var member in root.Members)
        {
            if (member is ClassDeclarationSyntax type &&
                string.Equals(type.Identifier.Text, ProbeClassName, StringComparison.Ordinal))
            {
                return type;
            }
        }

        return null;
    }

    // Fields, properties, and methods are the author-facing declarations completion offers;
    // constructors, events, operators, delegates, indexers, and nested types are skipped — the
    // same exclusions as the generator's binding classification. All accessibilities and
    // modifiers are offered: it is the author's own class body.
    private static void AppendMember(
        MemberDeclarationSyntax member,
        List<ScriptDeclaredMember> members)
    {
        switch (member)
        {
            case FieldDeclarationSyntax field:
            {
                var detail = field.Declaration.Type.ToString();
                var summary = GetDocumentationSummary(field);
                foreach (var variable in field.Declaration.Variables)
                {
                    members.Add(new ScriptDeclaredMember(
                        variable.Identifier.Text,
                        LanguageCompletionItemKind.Field,
                        detail,
                        summary));
                }

                break;
            }

            case PropertyDeclarationSyntax property:
                members.Add(new ScriptDeclaredMember(
                    property.Identifier.Text,
                    LanguageCompletionItemKind.Property,
                    property.Type.ToString(),
                    GetDocumentationSummary(property)));
                break;

            case MethodDeclarationSyntax method:
                members.Add(new ScriptDeclaredMember(
                    method.Identifier.Text,
                    LanguageCompletionItemKind.Method,
                    Normalize(
                        $"{method.ReturnType} {method.Identifier.Text}{method.ParameterList}"),
                    GetDocumentationSummary(method)));
                break;
        }
    }

    // The first surviving /// text line, cheaply: no XML document model, just trivia line scans
    // with inline <...> tag removal.
    private static string? GetDocumentationSummary(MemberDeclarationSyntax member)
    {
        foreach (var trivia in member.GetLeadingTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia))
            {
                continue;
            }

            foreach (var line in trivia.ToFullString().Split('\n'))
            {
                var text = StripDocumentationLine(line);
                if (text.Length > 0)
                {
                    return text;
                }
            }

            return null;
        }

        return null;
    }

    private static string StripDocumentationLine(string line)
    {
        var text = line.TrimStart();
        if (text.StartsWith("///", StringComparison.Ordinal))
        {
            text = text.Substring(3);
        }

        var builder = new StringBuilder(text.Length);
        var index = 0;
        while (index < text.Length)
        {
            if (text[index] == '<')
            {
                var close = text.IndexOf('>', index + 1);
                if (close < 0)
                {
                    break;
                }

                index = close + 1;
                continue;
            }

            builder.Append(text[index]);
            index++;
        }

        return builder.ToString().Trim();
    }

    // Collapses any whitespace run (including newlines from a multi-line parameter list) to a
    // single space so a method signature stays a one-line completion detail.
    private static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}
