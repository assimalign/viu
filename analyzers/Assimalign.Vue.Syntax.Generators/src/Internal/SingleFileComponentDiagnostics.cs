using Microsoft.CodeAnalysis;

using Assimalign.Vue.Syntax;

// The project namespace is nested under Assimalign.Vue.Syntax, so the base cluster's Diagnostic and
// DiagnosticSeverity are ambient and shadow Roslyn's; alias both sides for unambiguous mapping.
using SyntaxDiagnostic = Assimalign.Vue.Syntax.Diagnostic;
using SyntaxDiagnosticSeverity = Assimalign.Vue.Syntax.DiagnosticSeverity;
using RoslynDiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;

namespace Assimalign.Vue.Syntax.Generators;

/// <summary>
/// Maps the base <c>Assimalign.Vue.Syntax</c> <see cref="SyntaxDiagnostic"/> surface onto stable,
/// VUECS-prefixed Roslyn <see cref="DiagnosticDescriptor"/>s. The base deliberately keeps per-language
/// code catalogs (the <c>.viu</c> container's <c>SingleFileComponentErrorCode</c> starting at 1000, the
/// template compiler's upstream-pinned <c>CompilerErrorCode</c>); a generator cannot enumerate those
/// unbounded catalogs into one descriptor each without mirroring them, so this composition root instead
/// envelopes each diagnostic by its <em>origin</em> (the <c>.viu</c> block container vs a dispatched
/// <c>@template</c> parse) and its <see cref="SyntaxDiagnosticSeverity"/>, and carries the parser's
/// original message verbatim. The descriptor <c>defaultSeverity</c> follows the parser severity because
/// <c>Diagnostic.Create(descriptor, location, args)</c> reports at the descriptor's severity.
/// </summary>
internal static class SingleFileComponentDiagnostics
{
    private const string Category = "Assimalign.Vue.Syntax.Generators";

    /// <summary>A recoverable error reported by the <c>.viu</c> block-container parser.</summary>
    internal static readonly DiagnosticDescriptor SingleFileComponentError = new(
        id: "VUECS1001",
        title: "Single-file component parse error",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>A warning reported by the <c>.viu</c> block-container parser.</summary>
    internal static readonly DiagnosticDescriptor SingleFileComponentWarning = new(
        id: "VUECS1002",
        title: "Single-file component parse warning",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>An informational message reported by the <c>.viu</c> block-container parser.</summary>
    internal static readonly DiagnosticDescriptor SingleFileComponentInformation = new(
        id: "VUECS1003",
        title: "Single-file component parse information",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <summary>A recoverable error reported by the dispatched <c>@template</c> parse.</summary>
    internal static readonly DiagnosticDescriptor TemplateError = new(
        id: "VUECS1101",
        title: "Single-file component template parse error",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>A warning reported by the dispatched <c>@template</c> parse.</summary>
    internal static readonly DiagnosticDescriptor TemplateWarning = new(
        id: "VUECS1102",
        title: "Single-file component template parse warning",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>An informational message reported by the dispatched <c>@template</c> parse.</summary>
    internal static readonly DiagnosticDescriptor TemplateInformation = new(
        id: "VUECS1103",
        title: "Single-file component template parse information",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Info,
        isEnabledByDefault: true);

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
        return new DiagnosticInfo(descriptor, location, diagnostic.Message);
    }

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
        var (startLine, startCharacter) = Compose(blockStart, location.Start);
        var (endLine, endCharacter) = Compose(blockStart, location.End);
        return new LocationInfo(
            filePath,
            blockStart.Offset + location.Start.Offset,
            blockStart.Offset + location.End.Offset,
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
}
