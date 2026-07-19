using Microsoft.CodeAnalysis;

using Assimalign.Viu.Syntax;

// The project namespace is nested under Assimalign.Viu.Syntax, so the base cluster's Diagnostic and
// DiagnosticSeverity are ambient and shadow Roslyn's; alias both sides for unambiguous mapping.
using SyntaxDiagnostic = Assimalign.Viu.Syntax.Diagnostic;
using SyntaxDiagnosticSeverity = Assimalign.Viu.Syntax.DiagnosticSeverity;
using RoslynDiagnostic = Microsoft.CodeAnalysis.Diagnostic;
using RoslynDiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;

namespace Assimalign.Viu.Syntax.Generators;

/// <summary>
/// Maps the base <c>Assimalign.Viu.Syntax</c> <see cref="SyntaxDiagnostic"/> surface onto stable,
/// VIU-prefixed Roslyn <see cref="DiagnosticDescriptor"/>s. The base deliberately keeps per-language
/// code catalogs (the <c>.viu</c> container's <c>SingleFileComponentErrorCode</c> starting at 1000, the
/// template compiler's upstream-pinned <c>CompilerErrorCode</c>); a generator cannot enumerate those
/// unbounded catalogs into one descriptor each without mirroring them, so this composition root instead
/// envelopes each diagnostic by its <em>origin</em> (the <c>.viu</c> block container, a dispatched
/// <c>@template</c> parse, or the Roslyn parse of the <c>@script</c> block's C# — [V01.01.06.03]) and its
/// severity, and carries the parser's original message verbatim. The descriptor <c>defaultSeverity</c>
/// follows the parser severity because <c>Diagnostic.Create(descriptor, location, args)</c> reports at the
/// descriptor's severity.
/// </summary>
internal static class SingleFileComponentDiagnostics
{
    private const string Category = "Assimalign.Viu.Syntax.Generators";

    // The stable per-id help-link target: the VIU diagnostic catalog documents every descriptor's ID,
    // origin, severity, and configuration ([V01.01.05.08]). Each descriptor links its own heading anchor.
    private const string HelpLinkBase =
        "https://github.com/assimalign/vuecs/blob/main/analyzers/Assimalign.Viu.Syntax.Generators/docs/DIAGNOSTICS.md";

    private static string HelpLink(string id) => HelpLinkBase + "#" + id.ToLowerInvariant();

    /// <summary>A recoverable error reported by the <c>.viu</c> block-container parser.</summary>
    internal static readonly DiagnosticDescriptor SingleFileComponentError = new(
        id: "VIU1001",
        title: "Single-file component parse error",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1001"));

    /// <summary>A warning reported by the <c>.viu</c> block-container parser.</summary>
    internal static readonly DiagnosticDescriptor SingleFileComponentWarning = new(
        id: "VIU1002",
        title: "Single-file component parse warning",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1002"));

    /// <summary>An informational message reported by the <c>.viu</c> block-container parser.</summary>
    internal static readonly DiagnosticDescriptor SingleFileComponentInformation = new(
        id: "VIU1003",
        title: "Single-file component parse information",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1003"));

    /// <summary>A recoverable error reported by the dispatched <c>@template</c> parse.</summary>
    internal static readonly DiagnosticDescriptor TemplateError = new(
        id: "VIU1101",
        title: "Single-file component template parse error",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1101"));

    /// <summary>A warning reported by the dispatched <c>@template</c> parse.</summary>
    internal static readonly DiagnosticDescriptor TemplateWarning = new(
        id: "VIU1102",
        title: "Single-file component template parse warning",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1102"));

    /// <summary>An informational message reported by the dispatched <c>@template</c> parse.</summary>
    internal static readonly DiagnosticDescriptor TemplateInformation = new(
        id: "VIU1103",
        title: "Single-file component template parse information",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1103"));

    /// <summary>A recoverable error reported by the Roslyn parse of the <c>@script</c> block's C#.</summary>
    internal static readonly DiagnosticDescriptor ScriptError = new(
        id: "VIU1201",
        title: "Single-file component script parse error",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1201"));

    /// <summary>A warning reported by the Roslyn parse of the <c>@script</c> block's C#.</summary>
    internal static readonly DiagnosticDescriptor ScriptWarning = new(
        id: "VIU1202",
        title: "Single-file component script parse warning",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1202"));

    /// <summary>An informational message reported by the Roslyn parse of the <c>@script</c> block's C#.</summary>
    internal static readonly DiagnosticDescriptor ScriptInformation = new(
        id: "VIU1203",
        title: "Single-file component script parse information",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1203"));

    /// <summary>A recoverable error reported by the dispatched <c>@style</c> CSS parse ([V01.01.06.04]).</summary>
    internal static readonly DiagnosticDescriptor StyleError = new(
        id: "VIU1301",
        title: "Single-file component style parse error",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1301"));

    /// <summary>A warning reported by the dispatched <c>@style</c> CSS parse ([V01.01.06.04]).</summary>
    internal static readonly DiagnosticDescriptor StyleWarning = new(
        id: "VIU1302",
        title: "Single-file component style parse warning",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1302"));

    /// <summary>An informational message reported by the dispatched <c>@style</c> CSS parse ([V01.01.06.04]).</summary>
    internal static readonly DiagnosticDescriptor StyleInformation = new(
        id: "VIU1303",
        title: "Single-file component style parse information",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1303"));

    /// <summary>
    /// Envelopes <paramref name="diagnostic"/> as a value-equatable <see cref="DiagnosticInfo"/> located
    /// on the <c>.viu</c> file at <paramref name="filePath"/>. When <paramref name="blockContentStart"/>
    /// is supplied, the diagnostic's block-content-relative position is composed into file coordinates;
    /// when it is <see langword="null"/> the position is already file-relative (a container diagnostic).
    /// </summary>
    /// <param name="filePath">The originating <c>.viu</c> file path.</param>
    /// <param name="diagnostic">The base parser diagnostic to map.</param>
    /// <param name="fromTemplate">Whether the diagnostic came from a dispatched <c>@template</c> parse.</param>
    /// <param name="blockContentStart">The file position where the dispatched block's content begins, or <see langword="null"/>.</param>
    /// <returns>The value-equatable diagnostic.</returns>
    public static DiagnosticInfo Create(
        string filePath,
        SyntaxDiagnostic diagnostic,
        bool fromTemplate,
        Position? blockContentStart)
    {
        var descriptor = Map(fromTemplate, diagnostic.Severity);
        var location = BuildLocation(filePath, diagnostic.Location, blockContentStart);

        // The base surface deliberately projects each language's unbounded code catalog as RawCode;
        // carrying it in the message keeps the upstream-pinned CompilerErrorCode / Viu-defined
        // SingleFileComponentErrorCode visible to consumers without minting one descriptor per code.
        var message = diagnostic.Message
            + " ("
            + (fromTemplate ? "template compiler code " : "single-file-component code ")
            + diagnostic.RawCode.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + ")";
        return new DiagnosticInfo(descriptor, location, message);
    }

    /// <summary>
    /// Envelopes a Roslyn <paramref name="diagnostic"/> from the <c>@script</c> block's parse
    /// ([V01.01.06.03]) as a value-equatable <see cref="DiagnosticInfo"/> located on the <c>.viu</c> file.
    /// The diagnostic's position is relative to the synthetic probe wrapper
    /// (<see cref="ScriptBlockAnalyzer"/>); it is un-shifted to the block content by
    /// <paramref name="probePrefixLength"/>/<paramref name="probeLineOffset"/> and then composed with
    /// <paramref name="blockContentStart"/> into file coordinates through the <em>same</em>
    /// <see cref="Compose"/> arithmetic the dispatched-block path uses — so a script error lands on the
    /// exact <c>.viu</c> line/column the emitted <c>#line</c> directive maps to.
    /// </summary>
    /// <param name="filePath">The originating <c>.viu</c> file path.</param>
    /// <param name="diagnostic">The Roslyn parse diagnostic to map.</param>
    /// <param name="blockContentStart">The file position where the <c>@script</c> block's content begins.</param>
    /// <param name="probePrefixLength">The wrapper prefix length, to un-shift content-relative offsets.</param>
    /// <param name="probeLineOffset">The wrapper's leading line count, to un-shift content-relative lines.</param>
    /// <returns>The value-equatable diagnostic located on the <c>.viu</c> file.</returns>
    public static DiagnosticInfo CreateScript(
        string filePath,
        RoslynDiagnostic diagnostic,
        Position blockContentStart,
        int probePrefixLength,
        int probeLineOffset)
    {
        var descriptor = MapScript(diagnostic.Severity);
        var lineSpan = diagnostic.Location.GetLineSpan();
        var span = diagnostic.Location.SourceSpan;

        // Wrapper-relative Roslyn positions (zero-based line/character) -> block-content-relative Position
        // (the base cluster's one-based line/column convention), the input Compose expects. Offsets are
        // clamped at zero so a diagnostic reported at the wrapper's own synthetic prefix (never expected —
        // the prefix is a fixed well-formed string) can never compose a negative TextSpan bound and throw.
        var relativeStart = new Position(
            System.Math.Max(0, span.Start - probePrefixLength),
            (lineSpan.StartLinePosition.Line - probeLineOffset) + 1,
            lineSpan.StartLinePosition.Character + 1);
        var relativeEnd = new Position(
            System.Math.Max(0, span.End - probePrefixLength),
            (lineSpan.EndLinePosition.Line - probeLineOffset) + 1,
            lineSpan.EndLinePosition.Character + 1);

        var location = ComposeBlockLocation(filePath, blockContentStart, relativeStart, relativeEnd);

        // Carry the Roslyn error code (e.g. CS1525) in the message, mirroring how the container/template
        // paths surface their per-language catalog codes.
        var message = diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)
            + " (C# script code "
            + diagnostic.Id
            + ")";
        return new DiagnosticInfo(descriptor, location, message);
    }

    /// <summary>
    /// Envelopes a dispatched <c>@style</c> CSS parse <paramref name="diagnostic"/> ([V01.01.06.04]) as a
    /// value-equatable <see cref="DiagnosticInfo"/> located on the <c>.viu</c> file. The CSS parser reports
    /// positions relative to the <c>@style</c> block's content, so they are composed with
    /// <paramref name="blockContentStart"/> into <c>.viu</c> coordinates through the <em>same</em>
    /// <see cref="ComposeBlockLocation"/> arithmetic the <c>@template</c>/<c>@script</c> paths use, landing
    /// a CSS error on the exact <c>.viu</c> style line/column.
    /// </summary>
    /// <param name="filePath">The originating <c>.viu</c> file path.</param>
    /// <param name="diagnostic">The base CSS parser diagnostic to map.</param>
    /// <param name="blockContentStart">The file position where the <c>@style</c> block's content begins.</param>
    /// <returns>The value-equatable diagnostic located on the <c>.viu</c> file.</returns>
    public static DiagnosticInfo CreateStyle(string filePath, SyntaxDiagnostic diagnostic, Position blockContentStart)
    {
        var descriptor = MapStyle(diagnostic.Severity);
        var location = BuildLocation(filePath, diagnostic.Location, blockContentStart);

        // Carry the Viu-defined CssErrorCode in the message, mirroring how the container/template/script
        // paths surface their per-language catalog codes.
        var message = diagnostic.Message
            + " (CSS code "
            + diagnostic.RawCode.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + ")";
        return new DiagnosticInfo(descriptor, location, message);
    }

    private static DiagnosticDescriptor MapStyle(SyntaxDiagnosticSeverity severity)
        // Info and Hidden collapse into the informational descriptor, matching the container/template
        // mapping's treatment of the low end.
        => severity switch
        {
            SyntaxDiagnosticSeverity.Error => StyleError,
            SyntaxDiagnosticSeverity.Warning => StyleWarning,
            _ => StyleInformation,
        };

    private static DiagnosticDescriptor Map(bool fromTemplate, SyntaxDiagnosticSeverity severity)
    {
        // Hidden collapses into the informational descriptor: the generator surfaces it rather than
        // dropping it, and no parser in the cluster emits Hidden today.
        if (fromTemplate)
        {
            return severity switch
            {
                SyntaxDiagnosticSeverity.Error => TemplateError,
                SyntaxDiagnosticSeverity.Warning => TemplateWarning,
                _ => TemplateInformation,
            };
        }

        return severity switch
        {
            SyntaxDiagnosticSeverity.Error => SingleFileComponentError,
            SyntaxDiagnosticSeverity.Warning => SingleFileComponentWarning,
            _ => SingleFileComponentInformation,
        };
    }

    private static DiagnosticDescriptor MapScript(RoslynDiagnosticSeverity severity)
        // Info and Hidden collapse into the informational descriptor: the generator surfaces the message
        // rather than dropping it, matching the container/template mapping's treatment of the low end.
        => severity switch
        {
            RoslynDiagnosticSeverity.Error => ScriptError,
            RoslynDiagnosticSeverity.Warning => ScriptWarning,
            _ => ScriptInformation,
        };

    private static LocationInfo BuildLocation(string filePath, SourceLocation location, Position? blockContentStart)
    {
        if (blockContentStart is not { } blockStart)
        {
            // Container diagnostic: the position is already relative to the whole .viu file.
            return new LocationInfo(
                filePath,
                location.Start.Offset,
                location.End.Offset,
                location.Start.Line - 1,
                location.Start.Column - 1,
                location.End.Line - 1,
                location.End.Column - 1);
        }

        // Dispatched-block diagnostic: the position is relative to the block's content, so compose it
        // with the block's content-start position to land on the correct .viu file coordinate. This is
        // the same block-to-file coordinate mapping [V01.01.06.03] performs for #line directives.
        return ComposeBlockLocation(filePath, blockStart, location.Start, location.End);
    }

    // Composes a block-content-relative span (one-based positions plus content-relative offsets) with the
    // block's content-start position into whole-.viu-file coordinates. Shared by the dispatched-block
    // (@template) path and the @script path so both — and the emitted #line directives — agree exactly.
    private static LocationInfo ComposeBlockLocation(
        string filePath,
        Position blockStart,
        Position relativeStart,
        Position relativeEnd)
    {
        var (startLine, startCharacter) = Compose(blockStart, relativeStart);
        var (endLine, endCharacter) = Compose(blockStart, relativeEnd);
        return new LocationInfo(
            filePath,
            blockStart.Offset + relativeStart.Offset,
            blockStart.Offset + relativeEnd.Offset,
            startLine,
            startCharacter,
            endLine,
            endCharacter);
    }

    private static (int Line, int Character) Compose(Position blockStart, Position relative)
    {
        // Both positions are one-based (line/column); return zero-based for Roslyn. On the block's first
        // line the columns add; on later lines the relative column is already absolute for its line.
        var line = blockStart.Line + (relative.Line - 1);
        var column = relative.Line == 1
            ? blockStart.Column + (relative.Column - 1)
            : relative.Column;
        return (line - 1, column - 1);
    }

    /// <summary>
    /// Composes a block-content-relative template position into whole-<c>.viu</c>-file coordinates for a C#
    /// <c>#line</c> span directive ([V01.01.05.08] render source mapping), reusing the <em>same</em>
    /// <see cref="Compose"/> arithmetic the <c>@template</c>/<c>@script</c> diagnostic paths use so the
    /// emitted <c>#line</c> map and the reported diagnostics agree exactly. Returns one-based line/column
    /// (the <c>#line</c> directive convention), where <see cref="Compose"/> yields zero-based for Roslyn.
    /// </summary>
    /// <param name="blockContentStart">The file position where the block's content begins.</param>
    /// <param name="relative">The block-content-relative template position.</param>
    /// <returns>The one-based file line and column.</returns>
    internal static (int Line, int Column) ComposeToFilePosition(Position blockContentStart, Position relative)
    {
        var (line, character) = Compose(blockContentStart, relative);
        return (line + 1, character + 1);
    }
}
