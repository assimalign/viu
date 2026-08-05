using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using Assimalign.Viu.Syntax;
using Assimalign.Viu.Syntax.SingleFileComponent;
using Assimalign.Viu.Syntax.Templates;

namespace Assimalign.Viu.Tooling.SingleFileComponent;

/// <summary>
/// Analyzes a parsed <c>@script</c> block's C# for emission into a partial-class body. It does two jobs,
/// both purely syntactic
/// (the build host has no semantic model for code it is itself generating, and reflection is forbidden):
/// <list type="number">
/// <item>
/// <b>Region split ([V01.01.06.03.01]).</b> Leading <c>using</c> directives are separated from the class
/// members: a raw compilation-unit parse locates the leading using run, and the block is cut at the line
/// boundary after it into a hoisted <em>using region</em> and a class-body <em>member region</em>
/// (<see cref="ScriptRegions"/>). The split is forced by C#: a top-level <c>using</c> directive and a
/// bare member declaration have no common legal syntactic context.
/// </item>
/// <item>
/// <b>Validation.</b> Each region is Roslyn-parsed in the context the emitter places it — the using region
/// bare (a compilation unit, where usings are legal), the member region <em>inside a synthetic partial
/// class</em> (where members are legal) — so malformed C# in either surfaces as a recoverable,
/// position-mapped diagnostic rather than a crash or a broken generated file. Positions are composed back
/// to the <c>.viu</c> file by <see cref="SingleFileComponentDiagnostics.CreateScript"/> against each
/// region's own content start, agreeing with the block-to-file mapping used for dispatched-block
/// diagnostics and with the emitter's two <c>#line</c> anchors by construction.
/// </item>
/// <item>
/// <b>Binding-metadata extraction.</b> Each top-level member is classified into a <see cref="BindingType"/>,
/// driving where the template compiler inserts
/// <c>.Value</c>. Classification is conservative: only a field/property whose declared type is a known
/// <c>Assimalign.Viu.Reactivity</c> reference contract or implementation becomes
/// <see cref="BindingType.SetupReference"/> (the
/// only binding the template ever unwraps), so a misclassification can never ship a wrong <c>.Value</c>.
/// It is unaffected by the region split — only members are classified, exactly as if the usings were absent.
/// </item>
/// </list>
/// The editor shares this exact core through <see cref="DescribeMembers"/> ([V01.01.06.11]) and
/// <see cref="ParseProbe"/> ([V01.01.12.07.10]): the probe wrapper, the leading-using split, and member
/// discovery exist <em>once</em>, killing the drift the language service's hand-mirrored reader used to
/// carry.
/// See <c>docs/FORMAT.md</c> for the <c>@script</c> content contract. Work items [V01.01.06.03] and
/// [V01.01.06.03.01].
/// </summary>
internal static class ScriptBlockAnalyzer
{
    // The raw @script content is parsed wrapped in this synthetic partial class so Roslyn parses it in
    // the same syntactic context the emitter places it — a partial-class body. Members (fields,
    // properties, methods) are therefore legal; a stray using directive or malformed member surfaces as
    // the mapped diagnostic the consumer's own compile would also raise. The two-newline prefix puts the
    // first content line at wrapper line index 2 (zero-based), so a wrapper-relative line maps to a
    // content-relative line by subtracting ProbeLineOffset; content columns are unchanged because each
    // content line keeps its own leading whitespace (nothing is prepended per line). Shared by Analyze
    // (the build path) and DescribeMembers (the editor path) — the single source of the probe rules.
    private const string ProbePrefix = "partial class __ViuScriptProbe\n{\n";
    private const string ProbeSuffix = "\n}\n";
    private const int ProbeLineOffset = 2;
    private const string ContextMemberName = "Context";
    private const string SetupMemberName = "OnSetup";

    // The stable analyzer parse options: the repo compiles with LangVersion=preview, so the script is
    // validated against the same language surface it will ultimately be compiled under.
    // DocumentationMode.None on the build path — switching it to Parse would add doc-comment trivia cost
    // and potentially new XML-doc diagnostics to every build.
    private static readonly CSharpParseOptions ParseOptions =
        new(LanguageVersion.Preview, DocumentationMode.None, SourceCodeKind.Regular);

    // The editor parse options: DocumentationMode.Parse — deliberately different from the build path's
    // None — because DescribeMembers wants the /// summary text as structured trivia. The divergence is a
    // parameter of the shared split/probe core, never a second implementation of the rules.
    private static readonly CSharpParseOptions DescribeParseOptions =
        new(LanguageVersion.Preview, DocumentationMode.Parse, SourceCodeKind.Regular);

    // The Assimalign.Viu.Reactivity reference-carrying contracts and implementations. A field or property
    // of one of these holds a reactive reference the template compiler must unwrap through .Value
    // (BindingType.SetupReference). Matched by simple type name because classification is syntactic; the
    // projection references no runtime library.
    private static readonly HashSet<string> ReferenceTypeNames = new(StringComparer.Ordinal)
    {
        "IReactiveReference",
        "IReactiveTrackedReference",
        "Reference",
        "ShallowReference",
        "CustomReference",
        "Computed",
        "ReactiveValue",
    };

    /// <summary>
    /// Splits <paramref name="script"/> into its hoisted-using and class-body member regions, validates
    /// each, and extracts its binding metadata, appending any parse diagnostics (mapped to
    /// <paramref name="filePath"/> coordinates) to <paramref name="diagnostics"/>.
    /// </summary>
    /// <param name="filePath">The originating <c>.viu</c> file path (the diagnostic and <c>#line</c> anchor).</param>
    /// <param name="script">The parsed <c>@script</c> block.</param>
    /// <param name="diagnostics">The diagnostic accumulator the mapped script diagnostics are added to.</param>
    /// <param name="reservesGeneratedMembers">
    /// Whether the containing file has a template and therefore reserves <c>Context</c> and
    /// <c>OnSetup</c> for the generated <c>IComponentTemplate</c> bridge.
    /// </param>
    /// <returns>The regions to emit and the value-equatable classified bindings (empty when the block declares no members).</returns>
    public static ScriptAnalysis Analyze(
        string filePath,
        SingleFileComponentScriptBlock script,
        List<DiagnosticInfo> diagnostics,
        bool reservesGeneratedMembers = false)
    {
        var content = script.Content;
        var contentStart = script.ContentLocation.Start;

        string? usingRegion = null;
        var usingRegionStartLine = 0;
        var usingRegionStartColumn = 0;
        var memberRegionText = content;
        var memberRegionStartLine = contentStart.Line;
        var memberRegionStartColumn = contentStart.Column;
        var memberRegionOffset = 0;

        if (LocateLeadingUsingSplit(content, ParseOptions) is { } split)
        {
            usingRegion = content.Substring(0, split.SplitOffset);
            usingRegionStartLine = contentStart.Line;
            usingRegionStartColumn = contentStart.Column;
            memberRegionText = content.Substring(split.SplitOffset);
            memberRegionStartLine = contentStart.Line + split.MemberLineIndex;
            memberRegionStartColumn = 1;
            memberRegionOffset = split.SplitOffset;

            // Validate the hoisted using region. Parsed bare (no probe wrapper), its Roslyn positions are
            // already relative to the region's content start (= the script content start), so it composes
            // there — the same CreateScript/ComposeBlockLocation arithmetic the member region uses, so a
            // malformed hoisted using lands on the exact .viu coordinate.
            ValidateUsingRegion(usingRegion, filePath, contentStart, diagnostics);
        }

        // A whitespace-only member region declares no members and contributes no class-body text worth
        // emitting; treat it as absent so the emitter skips its #line block (and no probe parse runs).
        var memberRegion = string.IsNullOrWhiteSpace(memberRegionText) ? null : memberRegionText;

        var bindings = EquatableArray<ScriptBinding>.Empty;
        var declarations = ScriptDeclarations.None;
        if (memberRegion is not null)
        {
            // The member region begins at a .viu line boundary (column 1); compose diagnostics and the
            // classification against that position through the synthetic partial-class probe.
            var memberRegionStart = new Position(
                contentStart.Offset + memberRegionOffset,
                memberRegionStartLine,
                memberRegionStartColumn);
            bindings = ClassifyMembers(
                memberRegion,
                filePath,
                memberRegionStart,
                diagnostics,
                reservesGeneratedMembers,
                out declarations);
        }

        var regions = new ScriptRegions(
            usingRegion,
            usingRegionStartLine,
            usingRegionStartColumn,
            memberRegion,
            memberRegion is null ? 0 : memberRegionStartLine,
            memberRegion is null ? 0 : memberRegionStartColumn);
        return new ScriptAnalysis(regions, bindings, declarations);
    }

    /// <summary>
    /// Describes the fields, properties, and methods declared by <paramref name="scriptContent"/> for the
    /// editor ([V01.01.06.11]) — the same leading-using split and probe wrapper <see cref="Analyze"/>
    /// uses, parsed with <c>DocumentationMode.Parse</c> so <c>///</c> summaries surface as completion
    /// documentation. Parse diagnostics are ignored here: Roslyn error recovery still yields the
    /// well-formed members, and diagnostics remain <see cref="Analyze"/>'s job. Locations are
    /// block-content-relative (see <see cref="ScriptDeclaredMember"/>), so the description is a pure
    /// function of the script text and cacheable by content.
    /// </summary>
    /// <param name="scriptContent">The raw <c>@script</c> block content.</param>
    /// <returns>The declared members in declaration order (empty when the block declares none).</returns>
    public static EquatableArray<ScriptDeclaredMember> DescribeMembers(string scriptContent)
    {
        var parse = ParseProbe(scriptContent, DescribeParseOptions);
        if (parse.Probe is null)
        {
            return EquatableArray<ScriptDeclaredMember>.Empty;
        }

        var members = new List<ScriptDeclaredMember>();
        foreach (var member in parse.Probe.Members)
        {
            DescribeMember(
                member,
                parse.MemberRegionText,
                parse.MemberRegionOffset,
                parse.MemberRegionLineIndex,
                members);
        }

        return members.Count == 0
            ? EquatableArray<ScriptDeclaredMember>.Empty
            : new EquatableArray<ScriptDeclaredMember>(members.ToArray());
    }

    /// <summary>
    /// Parses <paramref name="scriptContent"/> in the synthetic partial-class probe and hands back the
    /// tree with the arithmetic that maps its offsets onto the block content
    /// ([V01.01.12.07.10]) — the same leading-using split and probe wrapper
    /// <see cref="DescribeMembers"/> uses, so a consumer that needs the syntax itself (folding walks the
    /// declared constructs rather than describing members) never re-derives the wrapping rules.
    /// Parse diagnostics are ignored here for the same reason they are in <see cref="DescribeMembers"/>:
    /// recovery still yields the well-formed constructs, and diagnostics remain <see cref="Analyze"/>'s
    /// job. Parsed with <c>DocumentationMode.None</c> — a syntax consumer reads no <c>///</c> text, so
    /// it does not pay for doc-comment trivia.
    /// </summary>
    /// <param name="scriptContent">The raw <c>@script</c> block content.</param>
    /// <returns>
    /// The parsed probe, or <see cref="ScriptProbeParse.None"/> when the block declares no members.
    /// </returns>
    public static ScriptProbeParse ParseProbe(string scriptContent)
        => ParseProbe(scriptContent, ParseOptions);

    // The shared split-and-wrap core behind DescribeMembers and ParseProbe. The parse options are the
    // one parameter that differs between the callers (the editor's member description wants /// trivia,
    // a syntax walk does not); every rule about WHERE the content is cut and HOW it is wrapped lives
    // here, once.
    private static ScriptProbeParse ParseProbe(string scriptContent, CSharpParseOptions parseOptions)
    {
        var memberRegionText = scriptContent;
        var memberRegionOffset = 0;
        var memberRegionLineIndex = 0;
        if (LocateLeadingUsingSplit(scriptContent, parseOptions) is { } split)
        {
            memberRegionText = scriptContent.Substring(split.SplitOffset);
            memberRegionOffset = split.SplitOffset;
            memberRegionLineIndex = split.MemberLineIndex;
        }

        if (string.IsNullOrWhiteSpace(memberRegionText))
        {
            return ScriptProbeParse.None;
        }

        var tree = CSharpSyntaxTree.ParseText(
            ProbePrefix + memberRegionText + ProbeSuffix,
            parseOptions);
        var probe = FindProbe((CompilationUnitSyntax)tree.GetRoot());
        return probe is null
            ? ScriptProbeParse.None
            : new ScriptProbeParse(
                probe,
                ProbePrefix.Length,
                memberRegionText,
                memberRegionOffset,
                memberRegionLineIndex);
    }

    // The line-boundary cut after the leading using run — THE region-split rule, shared by the build path
    // (DocumentationMode.None) and the editor path (DocumentationMode.Parse) so it exists once. In a
    // compilation unit the usings are legal top-level nodes (they are illegal in the member-region probe),
    // so this parse serves only to find the split point; member-region parse errors are validated by the
    // member probe, not collected here. C# forbids members before usings, so CompilationUnitSyntax.Usings
    // is exactly the leading run. A member sharing a line with the last using is a pathological, illegal
    // case that stays in the using region; it still surfaces a recoverable diagnostic rather than
    // crashing. Returns null when the block has no leading usings (the whole content is the member
    // region).
    private static (int SplitOffset, int MemberLineIndex)? LocateLeadingUsingSplit(
        string content,
        CSharpParseOptions parseOptions)
    {
        var splitTree = CSharpSyntaxTree.ParseText(content, parseOptions);
        var leadingUsings = ((CompilationUnitSyntax)splitTree.GetRoot()).Usings;
        if (leadingUsings.Count == 0)
        {
            return null;
        }

        // Split at the START of the first line after the last leading using directive, so both regions
        // begin at .viu column 1 and map cleanly under their own #line anchor.
        var lastUsing = leadingUsings[leadingUsings.Count - 1];
        var lastUsingEndLine = lastUsing.GetLocation().GetLineSpan().EndLinePosition.Line;
        var memberLineIndex = lastUsingEndLine + 1;
        var lines = splitTree.GetText().Lines;
        var splitOffset = memberLineIndex < lines.Count ? lines[memberLineIndex].Start : content.Length;
        return (splitOffset, memberLineIndex);
    }

    // Parses the class-body member region inside the synthetic partial-class probe (so member declarations
    // are legal), maps any parse diagnostics onto the .viu file against the member region's content start,
    // and classifies each top-level member into its binding type. The probe wrapper is un-shifted by
    // ProbePrefix.Length / ProbeLineOffset so a diagnostic composes to the member region's own coordinates.
    private static EquatableArray<ScriptBinding> ClassifyMembers(
        string memberRegion,
        string filePath,
        Position memberRegionStart,
        List<DiagnosticInfo> diagnostics,
        bool reservesGeneratedMembers,
        out ScriptDeclarations declarations)
    {
        declarations = ScriptDeclarations.None;
        var tree = CSharpSyntaxTree.ParseText(ProbePrefix + memberRegion + ProbeSuffix, ParseOptions);
        var root = (CompilationUnitSyntax)tree.GetRoot();

        foreach (var diagnostic in tree.GetDiagnostics())
        {
            diagnostics.Add(SingleFileComponentDiagnostics.CreateScript(
                filePath,
                diagnostic,
                memberRegionStart,
                ProbePrefix.Length,
                ProbeLineOffset));
        }

        var probe = FindProbe(root);
        if (probe is null)
        {
            return EquatableArray<ScriptBinding>.Empty;
        }

        // [CMP-26]/[CMP-30] The attribute-declared component surface. Only a template-bearing component
        // gets an IComponentTemplate bridge to hang the declarations on, so a style-only .viu never
        // reads them.
        if (reservesGeneratedMembers)
        {
            declarations = ComponentDeclarationReader.Read(
                probe,
                filePath,
                memberRegionStart,
                ProbePrefix.Length,
                ProbeLineOffset,
                diagnostics);
        }

        var bindings = new List<ScriptBinding>();
        foreach (var member in probe.Members)
        {
            ValidateGeneratedMemberContract(
                member,
                filePath,
                memberRegionStart,
                diagnostics,
                reservesGeneratedMembers);
            Classify(member, bindings);
        }

        return bindings.Count == 0
            ? EquatableArray<ScriptBinding>.Empty
            : new EquatableArray<ScriptBinding>(bindings.ToArray());
    }

    // Validates the hoisted using region as a bare compilation unit (usings are legal there) and maps any
    // recoverable parse diagnostics onto the .viu file against the region's content start. Parsed without
    // the probe wrapper, so the CreateScript un-shift offsets are zero — the same composition the member
    // probe uses, so a malformed hoisted using resolves to the exact .viu coordinate.
    private static void ValidateUsingRegion(
        string usingRegion,
        string filePath,
        Position regionStart,
        List<DiagnosticInfo> diagnostics)
    {
        var tree = CSharpSyntaxTree.ParseText(usingRegion, ParseOptions);
        foreach (var diagnostic in tree.GetDiagnostics())
        {
            diagnostics.Add(SingleFileComponentDiagnostics.CreateScript(
                filePath,
                diagnostic,
                regionStart,
                probePrefixLength: 0,
                probeLineOffset: 0));
        }
    }

    // Locates the synthetic wrapper class. It is normally the sole top-level member; a search by name is
    // used so malformed content that injects stray top-level declarations still finds the real members.
    private static ClassDeclarationSyntax? FindProbe(CompilationUnitSyntax root)
    {
        foreach (var member in root.Members)
        {
            if (member is ClassDeclarationSyntax type && string.Equals(type.Identifier.Text, "__ViuScriptProbe", StringComparison.Ordinal))
            {
                return type;
            }
        }

        return null;
    }

    // Describes one top-level member for the editor. Fields, properties, and methods are the
    // author-facing declarations completion and document symbols offer; constructors, events, operators,
    // delegates, indexers, and nested types are skipped — the same exclusions as the binding
    // classification. All accessibilities and modifiers are offered: it is the author's own class body.
    private static void DescribeMember(
        MemberDeclarationSyntax member,
        string memberRegionText,
        int memberRegionOffset,
        int memberRegionLineIndex,
        List<ScriptDeclaredMember> members)
    {
        switch (member)
        {
            case FieldDeclarationSyntax field:
            {
                var detail = field.Declaration.Type.ToString();
                var summary = GetDocumentationSummary(field);
                var memberLocation = DescribeLocation(
                    field.Span, memberRegionText, memberRegionOffset, memberRegionLineIndex);
                foreach (var variable in field.Declaration.Variables)
                {
                    members.Add(new ScriptDeclaredMember(
                        variable.Identifier.Text,
                        ScriptDeclaredMemberKind.Field,
                        detail,
                        summary,
                        memberLocation,
                        DescribeLocation(
                            variable.Identifier.Span, memberRegionText, memberRegionOffset, memberRegionLineIndex)));
                }

                break;
            }

            case PropertyDeclarationSyntax property:
                members.Add(new ScriptDeclaredMember(
                    property.Identifier.Text,
                    ScriptDeclaredMemberKind.Property,
                    property.Type.ToString(),
                    GetDocumentationSummary(property),
                    DescribeLocation(property.Span, memberRegionText, memberRegionOffset, memberRegionLineIndex),
                    DescribeLocation(
                        property.Identifier.Span, memberRegionText, memberRegionOffset, memberRegionLineIndex)));
                break;

            case MethodDeclarationSyntax method:
                members.Add(new ScriptDeclaredMember(
                    method.Identifier.Text,
                    ScriptDeclaredMemberKind.Method,
                    Normalize(
                        $"{method.ReturnType} {method.Identifier.Text}{method.ParameterList}"),
                    GetDocumentationSummary(method),
                    DescribeLocation(method.Span, memberRegionText, memberRegionOffset, memberRegionLineIndex),
                    DescribeLocation(
                        method.Identifier.Span, memberRegionText, memberRegionOffset, memberRegionLineIndex)));
                break;
        }
    }

    // Maps a probe-tree span to a block-content-relative SourceLocation: the wrapper prefix is un-shifted
    // by ProbePrefix.Length / ProbeLineOffset (the same arithmetic CreateScript uses for diagnostics), and
    // the member region's own cut (offset and cut line count) is re-added so positions are relative to the
    // WHOLE block content — the frame ComposeToFilePosition expects. Lines/columns are one-based (the base
    // cluster's Position convention); columns transfer verbatim because the member region begins at a line
    // boundary and the probe prepends nothing per line.
    private static SourceLocation DescribeLocation(
        TextSpan span,
        string memberRegionText,
        int memberRegionOffset,
        int memberRegionLineIndex)
    {
        // Both ends are clamped to the region because Roslyn error recovery can extend a member's span
        // past it into the probe's own suffix: an incomplete `using ` statement inside a method body
        // swallows the method's closing brace, so the described method runs to the wrapper's brace. The
        // editor reads declarations on every keystroke, and mid-keystroke text is exactly the text that
        // recovers this way, so an unclamped span throws while the author is simply typing.
        var regionStart = Math.Min(
            Math.Max(0, span.Start - ProbePrefix.Length),
            memberRegionText.Length);
        var regionEnd = Math.Min(
            Math.Max(regionStart, span.End - ProbePrefix.Length),
            memberRegionText.Length);
        var text = memberRegionText.Substring(regionStart, regionEnd - regionStart);

        var (startLine, startColumn) = RegionPosition(memberRegionText, regionStart);
        var (endLine, endColumn) = RegionPosition(memberRegionText, regionEnd);
        return new SourceLocation(
            new Position(
                memberRegionOffset + regionStart,
                memberRegionLineIndex + startLine,
                startColumn),
            new Position(
                memberRegionOffset + regionEnd,
                memberRegionLineIndex + endLine,
                endColumn),
            text);
    }

    // The one-based (line, column) of a character offset within the member region text. Counted directly
    // over the region text (not the probe tree) so the mapping cannot depend on the wrapper's own lines.
    private static (int Line, int Column) RegionPosition(string memberRegionText, int offset)
    {
        var line = 1;
        var lineStart = 0;
        for (var index = 0; index < offset && index < memberRegionText.Length; index++)
        {
            if (memberRegionText[index] == '\n')
            {
                line++;
                lineStart = index + 1;
            }
        }

        return (line, offset - lineStart + 1);
    }

    // The first surviving /// text line, cheaply: no XML document model, just trivia line scans with
    // inline <...> tag removal. Requires DescribeParseOptions (DocumentationMode.Parse) — under the build
    // path's DocumentationMode.None the trivia kind never appears.
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

    // Collapses any whitespace run (including newlines from a multi-line parameter list) to a single
    // space so a method signature stays a one-line completion detail.
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

    // Classifies one top-level member. Fields, properties, and methods are template-facing bindings;
    // constructors, events, nested types, operators, and the like are not, so they are skipped.
    private static void Classify(MemberDeclarationSyntax member, List<ScriptBinding> bindings)
    {
        switch (member)
        {
            case FieldDeclarationSyntax field:
            {
                var type = ClassifyField(field);
                foreach (var variable in field.Declaration.Variables)
                {
                    bindings.Add(new ScriptBinding(variable.Identifier.Text, type));
                }

                break;
            }

            case PropertyDeclarationSyntax property:
                bindings.Add(new ScriptBinding(property.Identifier.Text, ClassifyProperty(property)));
                break;

            case MethodDeclarationSyntax method:
                // A method is a fixed callable, never a reactive reference and never unwrapped — the
                // render path emits a direct call, not a .Value access.
                bindings.Add(new ScriptBinding(method.Identifier.Text, BindingType.SetupConstant));
                break;
        }
    }

    private static void ValidateGeneratedMemberContract(
        MemberDeclarationSyntax member,
        string filePath,
        Position memberRegionStart,
        List<DiagnosticInfo> diagnostics,
        bool reservesGeneratedMembers)
    {
        if (reservesGeneratedMembers)
        {
            foreach (var identifier in DeclaredIdentifiers(member))
            {
                if (string.Equals(identifier.Text, ContextMemberName, StringComparison.Ordinal) ||
                    (string.Equals(identifier.Text, SetupMemberName, StringComparison.Ordinal) &&
                     !IsSetupImplementation(member)))
                {
                    diagnostics.Add(SingleFileComponentDiagnostics.CreateScriptRule(
                        SingleFileComponentDiagnostics.ReservedScriptMember,
                        $"'{identifier.Text}' is reserved by the generated component scaffold. " +
                        (identifier.Text == SetupMemberName
                            ? "Implement it only as 'partial void OnSetup()'."
                            : "Access the generated IComponentContext through this member instead of declaring it."),
                        filePath,
                        identifier.GetLocation(),
                        memberRegionStart,
                        ProbePrefix.Length,
                        ProbeLineOffset));
                }
            }
        }

        if (member is MethodDeclarationSyntax method &&
            HasModifier(method.Modifiers, SyntaxKind.AsyncKeyword) &&
            method.ReturnType is PredefinedTypeSyntax returnType &&
            returnType.Keyword.IsKind(SyntaxKind.VoidKeyword))
        {
            diagnostics.Add(SingleFileComponentDiagnostics.CreateScriptRule(
                SingleFileComponentDiagnostics.AsynchronousVoidCallback,
                $"Asynchronous method '{method.Identifier.Text}' returns void and cannot be observed. " +
                "Return Task instead.",
                filePath,
                method.Identifier.GetLocation(),
                memberRegionStart,
                ProbePrefix.Length,
                ProbeLineOffset));
        }
    }

    private static IEnumerable<SyntaxToken> DeclaredIdentifiers(MemberDeclarationSyntax member)
    {
        switch (member)
        {
            case FieldDeclarationSyntax field:
                foreach (var variable in field.Declaration.Variables)
                {
                    yield return variable.Identifier;
                }

                break;
            case EventFieldDeclarationSyntax eventField:
                foreach (var variable in eventField.Declaration.Variables)
                {
                    yield return variable.Identifier;
                }

                break;
            case MethodDeclarationSyntax method:
                yield return method.Identifier;
                break;
            case PropertyDeclarationSyntax property:
                yield return property.Identifier;
                break;
            case EventDeclarationSyntax @event:
                yield return @event.Identifier;
                break;
            case BaseTypeDeclarationSyntax type:
                yield return type.Identifier;
                break;
            case DelegateDeclarationSyntax @delegate:
                yield return @delegate.Identifier;
                break;
        }
    }

    private static bool IsSetupImplementation(MemberDeclarationSyntax member)
    {
        if (member is not MethodDeclarationSyntax method ||
            !string.Equals(method.Identifier.Text, SetupMemberName, StringComparison.Ordinal) ||
            !HasModifier(method.Modifiers, SyntaxKind.PartialKeyword) ||
            method.ReturnType is not PredefinedTypeSyntax returnType ||
            !returnType.Keyword.IsKind(SyntaxKind.VoidKeyword) ||
            method.ParameterList.Parameters.Count != 0 ||
            method.TypeParameterList is not null ||
            method.ExplicitInterfaceSpecifier is not null ||
            (method.Body is null && method.ExpressionBody is null))
        {
            return false;
        }

        foreach (var modifier in method.Modifiers)
        {
            if (!modifier.IsKind(SyntaxKind.PartialKeyword) &&
                !modifier.IsKind(SyntaxKind.PrivateKeyword) &&
                !modifier.IsKind(SyntaxKind.AsyncKeyword))
            {
                return false;
            }
        }

        return true;
    }

    // Field classification, checked in precedence order so a wrong .Value decision can never ship:
    //   reference type          -> SetupReference   (SETUP_REF; the only unwrapped binding)
    //   const                   -> LiteralConstant  (LITERAL_CONST; a C# compile-time literal, folded)
    //   readonly (non-reference) -> SetupConstant    (SETUP_CONST; fixed after construction, never unwrapped)
    //   otherwise (mutable)     -> SetupLet         (SETUP_LET; a reassignable binding, never unwrapped)
    // The finer SETUP_MAYBE_REF / SETUP_REACTIVE_CONST buckets need reassignment/factory-call flow analysis
    // over a semantic model the projection cannot obtain here, so they are deliberately not produced.
    private static BindingType ClassifyField(FieldDeclarationSyntax field)
    {
        if (IsReferenceType(field.Declaration.Type))
        {
            return BindingType.SetupReference;
        }

        if (HasModifier(field.Modifiers, SyntaxKind.ConstKeyword))
        {
            return BindingType.LiteralConstant;
        }

        return HasModifier(field.Modifiers, SyntaxKind.ReadOnlyKeyword)
            ? BindingType.SetupConstant
            : BindingType.SetupLet;
    }

    // Property classification: a reference-typed property is unwrapped (SETUP_REF); otherwise a property
    // with a set accessor is a reassignable binding (SETUP_LET) and a get-only/init-only property is fixed
    // (SETUP_CONST). Neither non-reference case is ever unwrapped.
    private static BindingType ClassifyProperty(PropertyDeclarationSyntax property)
    {
        if (IsReferenceType(property.Type))
        {
            return BindingType.SetupReference;
        }

        return IsWritable(property) ? BindingType.SetupLet : BindingType.SetupConstant;
    }

    private static bool IsWritable(PropertyDeclarationSyntax property)
    {
        // An expression-bodied property (`=> value`) is get-only. Otherwise a `set` accessor makes it
        // reassignable; an `init` accessor does not (it is fixed after object initialization).
        if (property.AccessorList is null)
        {
            return false;
        }

        foreach (var accessor in property.AccessorList.Accessors)
        {
            if (accessor.IsKind(SyntaxKind.SetAccessorDeclaration))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsReferenceType(TypeSyntax type)
    {
        var name = GetSimpleTypeName(type);
        return name is not null && ReferenceTypeNames.Contains(name);
    }

    // The right-most simple identifier of a type reference, unwrapping nullable annotations and namespace
    // qualification so `IReactiveReference<int>`,
    // `Assimalign.Viu.Reactivity.IReactiveReference<int>`, and `Reference<int>?` all classify.
    private static string? GetSimpleTypeName(TypeSyntax type)
        => type switch
        {
            NullableTypeSyntax nullable => GetSimpleTypeName(nullable.ElementType),
            GenericNameSyntax generic => generic.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            QualifiedNameSyntax qualified => GetSimpleTypeName(qualified.Right),
            AliasQualifiedNameSyntax alias => GetSimpleTypeName(alias.Name),
            _ => null,
        };

    private static bool HasModifier(SyntaxTokenList modifiers, SyntaxKind kind)
    {
        foreach (var modifier in modifiers)
        {
            if (modifier.IsKind(kind))
            {
                return true;
            }
        }

        return false;
    }
}
