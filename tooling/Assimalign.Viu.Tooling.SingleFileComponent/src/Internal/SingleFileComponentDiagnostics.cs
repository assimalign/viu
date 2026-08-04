using System.Collections.Generic;

using Assimalign.Viu.Syntax;

// The base cluster's Diagnostic and DiagnosticSeverity are ambient through the Assimalign.Viu.Syntax
// using and would shadow Roslyn's; alias both sides for unambiguous mapping.
using SyntaxDiagnostic = Assimalign.Viu.Syntax.Diagnostic;
using SyntaxDiagnosticSeverity = Assimalign.Viu.Syntax.DiagnosticSeverity;
using RoslynDiagnostic = Microsoft.CodeAnalysis.Diagnostic;
using RoslynDiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;
using RoslynLocation = Microsoft.CodeAnalysis.Location;

namespace Assimalign.Viu.Tooling.SingleFileComponent;

/// <summary>
/// Maps the base <c>Assimalign.Viu.Syntax</c> <see cref="SyntaxDiagnostic"/> surface onto the stable,
/// VIU-prefixed host-neutral catalog ([V01.01.06.11]). The base deliberately keeps per-language
/// code catalogs (the <c>.viu</c> container's <c>SingleFileComponentErrorCode</c> starting at 1000, and the
/// template compiler's own <c>CompilerErrorCode</c>); the projection cannot enumerate those
/// unbounded catalogs into one descriptor each without mirroring them, so it instead envelopes each
/// diagnostic by its <em>origin</em> (the <c>.viu</c> block container, a dispatched template parse, the
/// Roslyn parse of the <c>@script</c> block's C# — [V01.01.06.03] — or a dispatched style CSS parse) and
/// its severity, and carries the parser's original message verbatim. Projection-owned script-contract
/// rules use the same location snapshot and mapping path under the reserved VIU1204+ range. Roslyn types
/// appear only as <em>inputs</em> (the script probe parse produces Roslyn diagnostics inside this
/// library); the output is the neutral <see cref="DiagnosticInfo"/> both hosts consume — the generator's
/// adapter owns the Roslyn descriptors so RS2008 release tracking stays in the analyzer project.
/// </summary>
internal static class SingleFileComponentDiagnostics
{
    // The stable per-id help-link target: the VIU diagnostic catalog documents every descriptor's ID,
    // origin, severity, and configuration ([V01.01.05.08]). Each descriptor links its own heading anchor.
    private const string HelpLinkBase =
        "https://github.com/assimalign/viu/blob/main/analyzers/Assimalign.Viu.Generators.Syntax/docs/DIAGNOSTICS.md";

    private static string HelpLink(string id) => HelpLinkBase + "#" + id.ToLowerInvariant();

    /// <summary>A recoverable error reported by the <c>.viu</c> block-container parser.</summary>
    internal static readonly SingleFileComponentDiagnosticDescriptor SingleFileComponentError = new(
        Id: "VIU1001",
        Title: "Single-file component parse error",
        DefaultSeverity: SingleFileComponentDiagnosticSeverity.Error,
        HelpLink: HelpLink("VIU1001"));

    /// <summary>A warning reported by the <c>.viu</c> block-container parser.</summary>
    internal static readonly SingleFileComponentDiagnosticDescriptor SingleFileComponentWarning = new(
        Id: "VIU1002",
        Title: "Single-file component parse warning",
        DefaultSeverity: SingleFileComponentDiagnosticSeverity.Warning,
        HelpLink: HelpLink("VIU1002"));

    /// <summary>An informational message reported by the <c>.viu</c> block-container parser.</summary>
    internal static readonly SingleFileComponentDiagnosticDescriptor SingleFileComponentInformation = new(
        Id: "VIU1003",
        Title: "Single-file component parse information",
        DefaultSeverity: SingleFileComponentDiagnosticSeverity.Information,
        HelpLink: HelpLink("VIU1003"));

    /// <summary>A compatibility <c>.vue</c> file is shadowed by its canonical same-base <c>.viu</c> source.</summary>
    internal static readonly SingleFileComponentDiagnosticDescriptor ConflictingComponentFormats = new(
        Id: "VIU1004",
        Title: "Conflicting single-file component formats",
        DefaultSeverity: SingleFileComponentDiagnosticSeverity.Error,
        HelpLink: HelpLink("VIU1004"));

    /// <summary>A recoverable error reported by the dispatched template parse.</summary>
    internal static readonly SingleFileComponentDiagnosticDescriptor TemplateError = new(
        Id: "VIU1101",
        Title: "Single-file component template parse error",
        DefaultSeverity: SingleFileComponentDiagnosticSeverity.Error,
        HelpLink: HelpLink("VIU1101"));

    /// <summary>A warning reported by the dispatched template parse.</summary>
    internal static readonly SingleFileComponentDiagnosticDescriptor TemplateWarning = new(
        Id: "VIU1102",
        Title: "Single-file component template parse warning",
        DefaultSeverity: SingleFileComponentDiagnosticSeverity.Warning,
        HelpLink: HelpLink("VIU1102"));

    /// <summary>An informational message reported by the dispatched template parse.</summary>
    internal static readonly SingleFileComponentDiagnosticDescriptor TemplateInformation = new(
        Id: "VIU1103",
        Title: "Single-file component template parse information",
        DefaultSeverity: SingleFileComponentDiagnosticSeverity.Information,
        HelpLink: HelpLink("VIU1103"));

    /// <summary>A recoverable error reported by the Roslyn parse of the <c>@script</c> block's C#.</summary>
    internal static readonly SingleFileComponentDiagnosticDescriptor ScriptError = new(
        Id: "VIU1201",
        Title: "Single-file component script parse error",
        DefaultSeverity: SingleFileComponentDiagnosticSeverity.Error,
        HelpLink: HelpLink("VIU1201"));

    /// <summary>A warning reported by the Roslyn parse of the <c>@script</c> block's C#.</summary>
    internal static readonly SingleFileComponentDiagnosticDescriptor ScriptWarning = new(
        Id: "VIU1202",
        Title: "Single-file component script parse warning",
        DefaultSeverity: SingleFileComponentDiagnosticSeverity.Warning,
        HelpLink: HelpLink("VIU1202"));

    /// <summary>An informational message reported by the Roslyn parse of the <c>@script</c> block's C#.</summary>
    internal static readonly SingleFileComponentDiagnosticDescriptor ScriptInformation = new(
        Id: "VIU1203",
        Title: "Single-file component script parse information",
        DefaultSeverity: SingleFileComponentDiagnosticSeverity.Information,
        HelpLink: HelpLink("VIU1203"));

    /// <summary>A script member conflicts with a member reserved by the generated component scaffold.</summary>
    internal static readonly SingleFileComponentDiagnosticDescriptor ReservedScriptMember = new(
        Id: "VIU1204",
        Title: "Single-file component script member is reserved",
        DefaultSeverity: SingleFileComponentDiagnosticSeverity.Error,
        HelpLink: HelpLink("VIU1204"));

    /// <summary>An asynchronous script callback returns void and therefore cannot be observed.</summary>
    internal static readonly SingleFileComponentDiagnosticDescriptor AsynchronousVoidCallback = new(
        Id: "VIU1205",
        Title: "Asynchronous void callback cannot be observed",
        DefaultSeverity: SingleFileComponentDiagnosticSeverity.Error,
        HelpLink: HelpLink("VIU1205"));

    /// <summary>A tag-based script block does not explicitly select Viu's C# script language.</summary>
    internal static readonly SingleFileComponentDiagnosticDescriptor UnsupportedScriptLanguage = new(
        Id: "VIU1206",
        Title: "Unsupported single-file component script language",
        DefaultSeverity: SingleFileComponentDiagnosticSeverity.Error,
        HelpLink: HelpLink("VIU1206"));

    /// <summary>
    /// A component declares the same kind of surface both by attribute and with its own
    /// <c>Parameters</c>/<c>Events</c> member ([CMP-31]).
    /// </summary>
    internal static readonly SingleFileComponentDiagnosticDescriptor ConflictingComponentDeclaration = new(
        Id: "VIU1207",
        Title: "Conflicting component parameter or event declaration",
        DefaultSeverity: SingleFileComponentDiagnosticSeverity.Error,
        HelpLink: HelpLink("VIU1207"));

    /// <summary>Two attributed members declare the same component parameter or event name.</summary>
    internal static readonly SingleFileComponentDiagnosticDescriptor DuplicateComponentDeclaration = new(
        Id: "VIU1208",
        Title: "Duplicate component parameter or event declaration",
        DefaultSeverity: SingleFileComponentDiagnosticSeverity.Error,
        HelpLink: HelpLink("VIU1208"));

    /// <summary>
    /// A <c>[Parameter]</c> or <c>[Event]</c> declaration is on a member shape the generated scaffold
    /// cannot implement, or carries a non-constant argument.
    /// </summary>
    internal static readonly SingleFileComponentDiagnosticDescriptor UnsupportedComponentDeclaration = new(
        Id: "VIU1209",
        Title: "Unsupported component parameter or event declaration",
        DefaultSeverity: SingleFileComponentDiagnosticSeverity.Error,
        HelpLink: HelpLink("VIU1209"));

    /// <summary>A recoverable error reported by the dispatched style CSS parse ([V01.01.06.04]).</summary>
    internal static readonly SingleFileComponentDiagnosticDescriptor StyleError = new(
        Id: "VIU1301",
        Title: "Single-file component style parse error",
        DefaultSeverity: SingleFileComponentDiagnosticSeverity.Error,
        HelpLink: HelpLink("VIU1301"));

    /// <summary>A warning reported by the dispatched style CSS parse ([V01.01.06.04]).</summary>
    internal static readonly SingleFileComponentDiagnosticDescriptor StyleWarning = new(
        Id: "VIU1302",
        Title: "Single-file component style parse warning",
        DefaultSeverity: SingleFileComponentDiagnosticSeverity.Warning,
        HelpLink: HelpLink("VIU1302"));

    /// <summary>An informational message reported by the dispatched style CSS parse ([V01.01.06.04]).</summary>
    internal static readonly SingleFileComponentDiagnosticDescriptor StyleInformation = new(
        Id: "VIU1303",
        Title: "Single-file component style parse information",
        DefaultSeverity: SingleFileComponentDiagnosticSeverity.Information,
        HelpLink: HelpLink("VIU1303"));

    /// <summary>
    /// Every entry of the neutral catalog, in id order. Each host's materialization must cover this list
    /// 1:1 — the generator's adapter-coverage test enumerates it so a new catalog entry without an
    /// adapter mapping fails a test rather than throwing at generation time.
    /// </summary>
    internal static IReadOnlyList<SingleFileComponentDiagnosticDescriptor> Catalog { get; } = new[]
    {
        SingleFileComponentError,
        SingleFileComponentWarning,
        SingleFileComponentInformation,
        ConflictingComponentFormats,
        TemplateError,
        TemplateWarning,
        TemplateInformation,
        ScriptError,
        ScriptWarning,
        ScriptInformation,
        ReservedScriptMember,
        AsynchronousVoidCallback,
        UnsupportedScriptLanguage,
        ConflictingComponentDeclaration,
        DuplicateComponentDeclaration,
        UnsupportedComponentDeclaration,
        StyleError,
        StyleWarning,
        StyleInformation,
    };

    /// <summary>
    /// Envelopes <paramref name="diagnostic"/> as a value-equatable <see cref="DiagnosticInfo"/> located
    /// on the <c>.viu</c> file at <paramref name="filePath"/>. When <paramref name="blockContentStart"/>
    /// is supplied, the diagnostic's block-content-relative position is composed into file coordinates;
    /// when it is <see langword="null"/> the position is already file-relative (a container diagnostic).
    /// </summary>
    /// <param name="filePath">The originating <c>.viu</c> file path.</param>
    /// <param name="diagnostic">The base parser diagnostic to map.</param>
    /// <param name="fromTemplate">Whether the diagnostic came from a dispatched template parse.</param>
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
        // carrying it in the message keeps the originating CompilerErrorCode / SingleFileComponentErrorCode
        // visible to consumers without minting one descriptor per code.
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

    /// <summary>Creates a projection-owned rule diagnostic on an exact source location.</summary>
    /// <param name="descriptor">The stable neutral diagnostic descriptor.</param>
    /// <param name="message">The diagnostic message.</param>
    /// <param name="filePath">The originating single-file-component path.</param>
    /// <param name="location">The exact file-relative source location.</param>
    /// <returns>The value-equatable located diagnostic.</returns>
    public static DiagnosticInfo CreateRule(
        SingleFileComponentDiagnosticDescriptor descriptor,
        string message,
        string filePath,
        SourceLocation location)
        => new(descriptor, BuildLocation(filePath, location, blockContentStart: null), message);

    /// <summary>Creates a projection-owned file-level rule diagnostic at the start of a source file.</summary>
    /// <param name="descriptor">The stable neutral diagnostic descriptor.</param>
    /// <param name="message">The diagnostic message.</param>
    /// <param name="filePath">The originating single-file-component path.</param>
    /// <returns>The value-equatable file-start diagnostic.</returns>
    public static DiagnosticInfo CreateFileRule(
        SingleFileComponentDiagnosticDescriptor descriptor,
        string message,
        string filePath)
        => new(descriptor, new LocationInfo(filePath, 0, 0, 0, 0, 0, 0), message);

    /// <summary>
    /// Creates a projection-owned script-rule diagnostic from a token located in the synthetic script
    /// probe, mapping it back to the originating <c>.viu</c> member region.
    /// </summary>
    /// <param name="descriptor">The projection-owned script diagnostic descriptor.</param>
    /// <param name="message">The diagnostic message.</param>
    /// <param name="filePath">The originating <c>.viu</c> file.</param>
    /// <param name="probeLocation">The token location inside the synthetic script probe.</param>
    /// <param name="memberRegionStart">The file position where the member region begins.</param>
    /// <param name="probePrefixLength">The synthetic probe prefix length.</param>
    /// <param name="probeLineOffset">The synthetic probe's leading line count.</param>
    /// <returns>The mapped, value-equatable diagnostic.</returns>
    public static DiagnosticInfo CreateScriptRule(
        SingleFileComponentDiagnosticDescriptor descriptor,
        string message,
        string filePath,
        RoslynLocation probeLocation,
        Position memberRegionStart,
        int probePrefixLength,
        int probeLineOffset)
    {
        var lineSpan = probeLocation.GetLineSpan();
        var span = probeLocation.SourceSpan;
        var relativeStart = new Position(
            System.Math.Max(0, span.Start - probePrefixLength),
            (lineSpan.StartLinePosition.Line - probeLineOffset) + 1,
            lineSpan.StartLinePosition.Character + 1);
        var relativeEnd = new Position(
            System.Math.Max(0, span.End - probePrefixLength),
            (lineSpan.EndLinePosition.Line - probeLineOffset) + 1,
            lineSpan.EndLinePosition.Character + 1);

        return new DiagnosticInfo(
            descriptor,
            ComposeBlockLocation(filePath, memberRegionStart, relativeStart, relativeEnd),
            message);
    }

    /// <summary>
    /// Envelopes a dispatched style CSS parse <paramref name="diagnostic"/> ([V01.01.06.04]) as a
    /// value-equatable <see cref="DiagnosticInfo"/> located on the <c>.viu</c> file. The CSS parser reports
    /// positions relative to the style block's content, so they are composed with
    /// <paramref name="blockContentStart"/> into <c>.viu</c> coordinates through the <em>same</em>
    /// <see cref="ComposeBlockLocation"/> arithmetic the template/<c>@script</c> paths use, landing
    /// a CSS error on the exact <c>.viu</c> style line/column.
    /// </summary>
    /// <param name="filePath">The originating <c>.viu</c> file path.</param>
    /// <param name="diagnostic">The base CSS parser diagnostic to map.</param>
    /// <param name="blockContentStart">The file position where the style block's content begins.</param>
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

    private static SingleFileComponentDiagnosticDescriptor MapStyle(SyntaxDiagnosticSeverity severity)
        // Info and Hidden collapse into the informational descriptor, matching the container/template
        // mapping's treatment of the low end.
        => severity switch
        {
            SyntaxDiagnosticSeverity.Error => StyleError,
            SyntaxDiagnosticSeverity.Warning => StyleWarning,
            _ => StyleInformation,
        };

    private static SingleFileComponentDiagnosticDescriptor Map(bool fromTemplate, SyntaxDiagnosticSeverity severity)
    {
        // Hidden collapses into the informational descriptor: the projection surfaces it rather than
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

    private static SingleFileComponentDiagnosticDescriptor MapScript(RoslynDiagnosticSeverity severity)
        // Info and Hidden collapse into the informational descriptor: the projection surfaces the message
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
    // (template) path and the @script path so both — and the emitted #line directives — agree exactly.
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
    /// <see cref="Compose"/> arithmetic the template/<c>@script</c> diagnostic paths use so the
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
