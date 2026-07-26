using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Assimalign.Viu.Syntax.SingleFileComponent;
using Assimalign.Viu.Tooling.UtilityCss;

namespace Assimalign.Viu.Sdk.Tasks;

/// <summary>
/// Compiles the literal utility candidates discovered in supported markup sources into the
/// consuming project's standalone <c>&lt;PackageId&gt;.utilities.css</c> bundle
/// ([V01.01.12.16], #159).
/// </summary>
/// <remarks>
/// The task is the file-system host around the shared deterministic source, theme, project
/// stylesheet, candidate, and utility compilers. It slices only template content from
/// <c>.viu</c> and tag-based <c>.vue</c> components, scans <c>.html</c> and <c>.htm</c> as host
/// markup, and never scans C# input. Candidate diagnostics are intentionally not promoted to
/// MSBuild warnings because source discovery encounters incidental plain-text tokens; malformed
/// CSS-first configuration and executable directives remain build diagnostics. Single-threaded;
/// invoked once per project build.
/// </remarks>
public sealed class ViuBundleUtilityCss : Microsoft.Build.Utilities.Task, ICancelableTask
{
    private const string DiscoveryMetadataName = "ViuUtilityCssDiscovery";
    private const string ExplicitDiscoveryMetadataValue = "Explicit";
    private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
    private readonly HashSet<string> _hotReloadDependencyFiles =
        new HashSet<string>(GetPathComparer());
    private readonly HashSet<string> _hotReloadSourceDirectories =
        new HashSet<string>(GetPathComparer());

    /// <summary>
    /// Gets or sets the host-discovered <c>.viu</c>, <c>.vue</c>, <c>.html</c>, and
    /// <c>.htm</c> source files. Other extensions are ignored, preserving the no-code-first
    /// boundary.
    /// </summary>
    public ITaskItem[] SourceFiles { get; set; } = Array.Empty<ITaskItem>();

    /// <summary>
    /// Gets or sets the optional project utility entry CSS. Exactly one entry is accepted; its
    /// top-level <c>@theme</c> blocks configure the same immutable theme used by candidate scanning
    /// and compilation.
    /// </summary>
    public ITaskItem[] UtilityStylesheets { get; set; } = Array.Empty<ITaskItem>();

    /// <summary>
    /// Gets or sets the consuming project directory used to resolve CSS-first source and reference
    /// paths.
    /// </summary>
    [Required]
    public string ProjectDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the absolute intermediate bundle path.
    /// </summary>
    [Required]
    public string BundleOutputPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether removing the final utility rule should replace an existing bundle with an
    /// empty stylesheet instead of deleting it. This is enabled only by the development watch worker so
    /// the browser's static-asset transport receives the removal.
    /// </summary>
    public bool PreserveEmptyBundleOnRemoval { get; set; }

    /// <summary>
    /// Gets or sets the marker path that identifies a watch-only empty utility bundle.
    /// </summary>
    public string EmptyBundleMarkerPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional intermediate manifest written with the resolved CSS references and
    /// external source roots that the development hot-reload worker must observe.
    /// </summary>
    public string HotReloadDependencyManifestPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets the bundle path that exists after execution, or the empty string when no supported
    /// candidate resolved to CSS.
    /// </summary>
    [Output]
    public string BundlePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets whether this execution wrote new bundle bytes. Deleting an obsolete bundle is not a
    /// write.
    /// </summary>
    [Output]
    public bool BundleWritten { get; set; }

    /// <summary>
    /// Gets whether the bundle exists after execution.
    /// </summary>
    [Output]
    public bool BundleExists { get; set; }

    /// <inheritdoc />
    public void Cancel() => _cancellation.Cancel();

    /// <inheritdoc />
    public override bool Execute()
    {
        try
        {
            return ExecuteCore();
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected build-host control flow. Preserve the output state observed
            // before cancellation and let MSBuild tear down without adding a misleading error.
            return !Log.HasLoggedErrors;
        }
        finally
        {
            WriteHotReloadDependencyManifest();
        }
    }

    private bool ExecuteCore()
    {
        _hotReloadDependencyFiles.Clear();
        _hotReloadSourceDirectories.Clear();
        BundleExists = File.Exists(BundleOutputPath);
        BundlePath = BundleExists ? BundleOutputPath : string.Empty;
        BundleWritten = false;

        if (!TryReadEntry(out var entry))
        {
            return false;
        }

        var sourceContents = new Dictionary<string, string>(
            GetPathComparer());
        if (!string.IsNullOrEmpty(entry.SourceIdentity))
        {
            sourceContents[entry.SourceIdentity] = entry.Css;
        }

        var sourceParse = UtilitySourceParser.Parse(
            entry.Css,
            new UtilitySourceParseOptions
            {
                SourceIdentity = entry.SourceIdentity,
            },
            _cancellation.Token);
        if (!LogSourceDiagnostics(
                sourceParse,
                entry,
                sourceContents))
        {
            return false;
        }

        if (!TryBuildReferenceGraph(
                entry,
                sourceContents,
                out var referenceGraph))
        {
            return false;
        }

        if (!TryParseTheme(
                entry,
                sourceParse.Configuration,
                referenceGraph,
                sourceContents,
                out var theme))
        {
            return false;
        }

        var projectOptions = new UtilityProjectStylesheetCompilationOptions
        {
            SourceIdentity = entry.SourceIdentity,
            Registry = UtilityCssRegistry.BuiltIn,
            Theme = theme,
            ReferenceGraph = referenceGraph,
        };
        var definitionCompilation =
            UtilityProjectStylesheetCompiler.Compile(
                entry.Css,
                Array.Empty<string>(),
                projectOptions,
                _cancellation.Token);
        var variantRegistry = CreateVariantRegistry(
            theme,
            definitionCompilation.Variants);
        var effectiveSourceParse = UtilitySourceParser.Parse(
            entry.Css,
            new UtilitySourceParseOptions
            {
                SourceIdentity = entry.SourceIdentity,
                Prefix = theme.Prefix,
                VariantRegistry = variantRegistry,
            },
            _cancellation.Token);

        var candidateTexts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in effectiveSourceParse.Configuration.IncludedCandidates)
        {
            candidateTexts.Add(candidate);
        }

        if (!TryResolveSourceFiles(
                entry,
                effectiveSourceParse.Configuration,
                out var sourceFiles))
        {
            return false;
        }

        foreach (var sourcePath in sourceFiles)
        {
            _cancellation.Token.ThrowIfCancellationRequested();
            if (!TryGetSupportedSourceKind(sourcePath, out var sourceKind))
            {
                continue;
            }

            string source;
            try
            {
                source = File.ReadAllText(sourcePath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Log.LogErrorFromException(exception, showStackTrace: false);
                return false;
            }

            var region = GetScanRegion(source, sourceKind);
            if (region is null)
            {
                continue;
            }

            var scan = UtilityCandidateScanner.Scan(
                region.Value.Text,
                new UtilityCandidateScanOptions
                {
                    SourceIdentity = sourcePath,
                    ContentOffset = region.Value.ContentOffset,
                    Prefix = theme.Prefix,
                    VariantRegistry = variantRegistry,
                },
                _cancellation.Token);

            foreach (var detection in scan.Candidates)
            {
                candidateTexts.Add(detection.Candidate.RawText);
            }
        }

        foreach (var candidate in effectiveSourceParse.Configuration.ExcludedCandidates)
        {
            candidateTexts.Remove(candidate);
        }

        var projectCompilation =
            UtilityProjectStylesheetCompiler.Compile(
                entry.Css,
                candidateTexts,
                projectOptions,
                _cancellation.Token);
        if (!LogProjectDiagnostics(
                projectCompilation,
                entry,
                sourceContents))
        {
            return false;
        }

        var projectCandidateTexts = new HashSet<string>(
            projectCompilation.Rules.Select(rule => rule.CandidateText),
            StringComparer.Ordinal);
        var builtInCompilation = UtilityCssCompiler.Compile(
            candidateTexts.Where(
                candidate => !projectCandidateTexts.Contains(candidate)),
            UtilityCssRegistry.BuiltIn,
            theme,
            _cancellation.Token);
        var rules = MergeRules(
            builtInCompilation.Rules,
            projectCompilation.Rules);

        if (rules.Count == 0 &&
            string.IsNullOrWhiteSpace(projectCompilation.AuthoredCss) &&
            !effectiveSourceParse.Configuration.HasUtilitiesImport)
        {
            return DeleteObsoleteBundle();
        }

        var utilityCss = RenderUtilityLayer(rules);
        var authoredSections = SplitCssPrologue(
            projectCompilation.AuthoredCss);
        var usageCss =
            authoredSections.Body +
            "\n" +
            utilityCss;
        var designSystemCss = UtilityCssLayerEmitter.EmitDesignSystem(
            theme,
            usageCss,
            UtilityThemeOptions.None,
            _cancellation.Token);
        var stylesheet = RenderStylesheet(
            authoredSections.Prologue,
            designSystemCss,
            authoredSections.Body,
            utilityCss);
        return WriteBundle(stylesheet);
    }

    private bool WriteBundle(string stylesheet)
    {
        try
        {
            DeleteEmptyBundleMarker();

            if (File.Exists(BundleOutputPath) &&
                string.Equals(
                    File.ReadAllText(BundleOutputPath),
                    stylesheet,
                    StringComparison.Ordinal))
            {
                BundlePath = BundleOutputPath;
                BundleExists = true;
                Log.LogMessage(
                    MessageImportance.Low,
                    "ViuBundleUtilityCss: utility bundle unchanged; skipped write of {0}.",
                    BundleOutputPath);
                return true;
            }

            var directory = Path.GetDirectoryName(BundleOutputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                BundleOutputPath,
                stylesheet,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            BundlePath = BundleOutputPath;
            BundleExists = true;
            BundleWritten = true;
            Log.LogMessage(
                MessageImportance.Normal,
                "ViuBundleUtilityCss: wrote utility bundle to {0}.",
                BundleOutputPath);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.LogErrorFromException(exception, showStackTrace: false);
            return false;
        }
    }

    private bool TryReadEntry(out UtilityStylesheetEntry entry)
    {
        var projectDirectory = Path.GetFullPath(ProjectDirectory);
        entry = new UtilityStylesheetEntry(
            string.Empty,
            string.Empty,
            projectDirectory);
        if (UtilityStylesheets.Length == 0)
        {
            return true;
        }

        if (UtilityStylesheets.Length != 1)
        {
            Log.LogError(
                "Viu Utilities accepts exactly one project utility entry through @(ViuUtilityCss). " +
                "Resolve imports into that entry instead of supplying multiple roots.");
            return false;
        }

        var sourcePath = ResolvePath(
            projectDirectory,
            GetItemPath(UtilityStylesheets[0]));
        _hotReloadDependencyFiles.Add(sourcePath);
        string source;
        try
        {
            source = File.ReadAllText(sourcePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.LogErrorFromException(exception, showStackTrace: false);
            return false;
        }

        entry = new UtilityStylesheetEntry(
            sourcePath,
            source,
            Path.GetDirectoryName(sourcePath) ?? projectDirectory);
        return true;
    }

    private bool TryBuildReferenceGraph(
        UtilityStylesheetEntry entry,
        IDictionary<string, string> sourceContents,
        out UtilityStylesheetReferenceGraph graph)
    {
        graph = UtilityStylesheetReferenceGraph.Empty;
        if (string.IsNullOrEmpty(entry.SourceIdentity))
        {
            return true;
        }

        var hasReadError = false;
        graph = UtilityStylesheetReferenceGraphBuilder.Build(
            entry.SourceIdentity,
            entry.Css,
            (referencingSourceIdentity, specifier) =>
            {
                _cancellation.Token.ThrowIfCancellationRequested();
                var referencingDirectory =
                    Path.GetDirectoryName(referencingSourceIdentity) ??
                    entry.BaseDirectory;
                var resolvedSourceIdentity = ResolvePath(
                    referencingDirectory,
                    specifier);
                _hotReloadDependencyFiles.Add(resolvedSourceIdentity);
                if (!File.Exists(resolvedSourceIdentity))
                {
                    return null;
                }

                try
                {
                    var css = File.ReadAllText(resolvedSourceIdentity);
                    sourceContents[resolvedSourceIdentity] = css;
                    return new UtilityStylesheetReference(
                        referencingSourceIdentity,
                        specifier,
                        resolvedSourceIdentity,
                        css);
                }
                catch (Exception exception)
                    when (exception is IOException or UnauthorizedAccessException)
                {
                    hasReadError = true;
                    Log.LogErrorFromException(
                        exception,
                        showStackTrace: false);
                    return null;
                }
            },
            _cancellation.Token);
        return !hasReadError;
    }

    private bool TryParseTheme(
        UtilityStylesheetEntry entry,
        UtilitySourceConfiguration sourceConfiguration,
        UtilityStylesheetReferenceGraph referenceGraph,
        IReadOnlyDictionary<string, string> sourceContents,
        out UtilityTheme theme)
    {
        theme = UtilityTheme.Default;
        foreach (var reference in GetThemeReferenceOrder(
                     entry.SourceIdentity,
                     referenceGraph))
        {
            _cancellation.Token.ThrowIfCancellationRequested();
            var referencedThemeParse = UtilityThemeParser.Parse(
                reference.Css,
                new UtilityThemeParseOptions
                {
                    BaseTheme = theme,
                    SourceIdentity = reference.ResolvedSourceIdentity,
                },
                _cancellation.Token);
            if (!LogThemeDiagnostics(
                    referencedThemeParse,
                    entry,
                    sourceContents))
            {
                return false;
            }

            theme = referencedThemeParse.Theme;
        }

        var rootThemeParse = UtilityThemeParser.Parse(
            entry.Css,
            new UtilityThemeParseOptions
            {
                BaseTheme = theme,
                Prefix = sourceConfiguration.Prefix,
                ImportedThemeOptions =
                    sourceConfiguration.ImportedThemeOptions,
                IsImportant = sourceConfiguration.IsImportant,
                SourceIdentity = entry.SourceIdentity,
            },
            _cancellation.Token);
        if (!LogThemeDiagnostics(
                rootThemeParse,
                entry,
                sourceContents))
        {
            return false;
        }

        theme = rootThemeParse.Theme;
        return true;
    }

    private static IReadOnlyList<UtilityStylesheetReference> GetThemeReferenceOrder(
        string rootSourceIdentity,
        UtilityStylesheetReferenceGraph referenceGraph)
    {
        if (string.IsNullOrEmpty(rootSourceIdentity) ||
            referenceGraph.References.Count == 0)
        {
            return Array.Empty<UtilityStylesheetReference>();
        }

        var referencesBySource = referenceGraph.References
            .GroupBy(
                reference => reference.ReferencingSourceIdentity,
                GetPathComparer())
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(reference => reference.Specifier, StringComparer.Ordinal)
                    .ToArray(),
                GetPathComparer());
        var visited = new HashSet<string>(
            GetPathComparer());
        var ordered = new List<UtilityStylesheetReference>();

        VisitThemeReferences(
            rootSourceIdentity,
            referencesBySource,
            visited,
            ordered);
        return ordered;
    }

    private static void VisitThemeReferences(
        string sourceIdentity,
        IReadOnlyDictionary<string, UtilityStylesheetReference[]> referencesBySource,
        ISet<string> visited,
        ICollection<UtilityStylesheetReference> ordered)
    {
        if (!visited.Add(sourceIdentity) ||
            !referencesBySource.TryGetValue(sourceIdentity, out var references))
        {
            return;
        }

        foreach (var reference in references)
        {
            if (visited.Contains(reference.ResolvedSourceIdentity))
            {
                continue;
            }

            VisitThemeReferences(
                reference.ResolvedSourceIdentity,
                referencesBySource,
                visited,
                ordered);
            ordered.Add(reference);
        }
    }

    private static CssSections SplitCssPrologue(string css)
    {
        var normalized = NormalizeLineEndings(css);
        var index = 0;
        var prologueEnd = 0;
        while (index < normalized.Length)
        {
            SkipCssTrivia(
                normalized,
                ref index);
            if (!StartsCssDirective(
                    normalized,
                    index,
                    "@charset") &&
                !StartsCssDirective(
                    normalized,
                    index,
                    "@import"))
            {
                break;
            }

            var statementEnd = FindCssStatementEnd(
                normalized,
                index);
            if (statementEnd < 0)
            {
                break;
            }

            index = statementEnd + 1;
            prologueEnd = index;
        }

        if (prologueEnd == 0)
        {
            return new CssSections(
                string.Empty,
                normalized);
        }

        index = prologueEnd;
        SkipCssTrivia(
            normalized,
            ref index);
        return new CssSections(
            normalized.Substring(0, index),
            normalized.Substring(index));
    }

    private static void SkipCssTrivia(
        string css,
        ref int index)
    {
        while (index < css.Length)
        {
            if (char.IsWhiteSpace(css[index]))
            {
                index++;
                continue;
            }

            if (index + 1 < css.Length &&
                css[index] == '/' &&
                css[index + 1] == '*')
            {
                var commentEnd = css.IndexOf(
                    "*/",
                    index + 2,
                    StringComparison.Ordinal);
                index = commentEnd < 0
                    ? css.Length
                    : commentEnd + 2;
                continue;
            }

            break;
        }
    }

    private static bool StartsCssDirective(
        string css,
        int index,
        string directive)
    {
        if (index + directive.Length > css.Length ||
            !string.Equals(
                css.Substring(
                    index,
                    directive.Length),
                directive,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var end = index + directive.Length;
        return end == css.Length ||
            char.IsWhiteSpace(css[end]) ||
            css[end] is '\'' or '"' or '(';
    }

    private static int FindCssStatementEnd(
        string css,
        int start)
    {
        var parenthesisDepth = 0;
        var index = start;
        while (index < css.Length)
        {
            if (index + 1 < css.Length &&
                css[index] == '/' &&
                css[index + 1] == '*')
            {
                var commentEnd = css.IndexOf(
                    "*/",
                    index + 2,
                    StringComparison.Ordinal);
                if (commentEnd < 0)
                {
                    return -1;
                }

                index = commentEnd + 2;
                continue;
            }

            if (css[index] is '\'' or '"')
            {
                var quote = css[index++];
                while (index < css.Length)
                {
                    if (css[index] == '\\' &&
                        index + 1 < css.Length)
                    {
                        index += 2;
                        continue;
                    }

                    if (css[index++] == quote)
                    {
                        break;
                    }
                }

                continue;
            }

            if (css[index] == '(')
            {
                parenthesisDepth++;
            }
            else if (css[index] == ')' &&
                     parenthesisDepth > 0)
            {
                parenthesisDepth--;
            }
            else if (css[index] == ';' &&
                     parenthesisDepth == 0)
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    private static string RenderStylesheet(
        string prologueCss,
        string designSystemCss,
        string authoredCss,
        string utilityCss)
    {
        var result = new StringBuilder();
        AppendCssSection(result, prologueCss);
        result.Append(designSystemCss);
        AppendCssSection(result, authoredCss);
        AppendCssSection(result, utilityCss);
        return result.ToString();
    }

    private static void AppendCssSection(
        StringBuilder result,
        string css)
    {
        var normalized = NormalizeLineEndings(css).Trim();
        if (normalized.Length == 0)
        {
            return;
        }

        if (result.Length > 0 &&
            result[result.Length - 1] != '\n')
        {
            result.Append('\n');
        }

        result.Append(normalized);
        result.Append('\n');
    }

    private bool LogSourceDiagnostics(
        UtilitySourceParseResult result,
        UtilityStylesheetEntry entry,
        IReadOnlyDictionary<string, string> sourceContents)
    {
        var hasErrors = false;
        foreach (var diagnostic in result.Diagnostics)
        {
            var isError =
                diagnostic.Severity == UtilitySourceDiagnosticSeverity.Error;
            hasErrors |= isError;
            LogLocatedDiagnostic(
                diagnostic.SourceSpan.SourceIdentity,
                diagnostic.SourceSpan.Start,
                diagnostic.SourceSpan.End,
                diagnostic.Message,
                isError,
                entry,
                sourceContents);
        }

        return !hasErrors;
    }

    private bool LogThemeDiagnostics(
        UtilityThemeParseResult result,
        UtilityStylesheetEntry entry,
        IReadOnlyDictionary<string, string> sourceContents)
    {
        var hasErrors = false;
        foreach (var diagnostic in result.Diagnostics)
        {
            var isError =
                diagnostic.Severity == UtilityThemeDiagnosticSeverity.Error;
            hasErrors |= isError;
            LogLocatedDiagnostic(
                diagnostic.SourceSpan.SourceIdentity,
                diagnostic.SourceSpan.Start,
                diagnostic.SourceSpan.End,
                diagnostic.Message,
                isError,
                entry,
                sourceContents);
        }

        return !hasErrors;
    }

    private bool LogProjectDiagnostics(
        UtilityProjectStylesheetCompilationResult result,
        UtilityStylesheetEntry entry,
        IReadOnlyDictionary<string, string> sourceContents)
    {
        var hasErrors = false;
        foreach (var diagnostic in result.DirectiveDiagnostics)
        {
            var isError =
                diagnostic.Severity == UtilityDirectiveDiagnosticSeverity.Error;
            hasErrors |= isError;
            LogLocatedDiagnostic(
                diagnostic.SourceSpan.SourceIdentity,
                diagnostic.SourceSpan.Start,
                diagnostic.SourceSpan.End,
                diagnostic.Message,
                isError,
                entry,
                sourceContents);
        }

        foreach (var diagnostic in result.Diagnostics)
        {
            var isError =
                diagnostic.Severity ==
                UtilityProjectStylesheetDiagnosticSeverity.Error;
            hasErrors |= isError;
            LogLocatedDiagnostic(
                diagnostic.SourceSpan.SourceIdentity,
                diagnostic.SourceSpan.Start,
                diagnostic.SourceSpan.End,
                diagnostic.Message,
                isError,
                entry,
                sourceContents);
        }

        return !hasErrors;
    }

    private void LogLocatedDiagnostic(
        string? sourceIdentity,
        int startOffset,
        int endOffset,
        string message,
        bool isError,
        UtilityStylesheetEntry entry,
        IReadOnlyDictionary<string, string> sourceContents)
    {
        var effectiveSourceIdentity =
            string.IsNullOrEmpty(sourceIdentity)
                ? entry.SourceIdentity
                : sourceIdentity;
        var source =
            effectiveSourceIdentity is not null &&
            effectiveSourceIdentity.Length != 0 &&
            sourceContents.TryGetValue(effectiveSourceIdentity, out var value)
                ? value
                : entry.Css;
        var start = GetLineAndColumn(
            source,
            startOffset);
        var end = GetLineAndColumn(
            source,
            endOffset);
        if (isError)
        {
            Log.LogError(
                "Viu Utilities",
                null,
                null,
                effectiveSourceIdentity,
                start.Line,
                start.Column,
                end.Line,
                end.Column,
                message);
        }
        else
        {
            Log.LogWarning(
                "Viu Utilities",
                null,
                null,
                effectiveSourceIdentity,
                start.Line,
                start.Column,
                end.Line,
                end.Column,
                message);
        }
    }

    private bool TryResolveSourceFiles(
        UtilityStylesheetEntry entry,
        UtilitySourceConfiguration configuration,
        out IReadOnlyList<string> sourceFiles)
    {
        var resolvedSources = new HashSet<string>(
            GetPathComparer());
        var projectDirectory = Path.GetFullPath(ProjectDirectory);
        try
        {
            foreach (var sourceFile in SourceFiles)
            {
                _cancellation.Token.ThrowIfCancellationRequested();
                if (sourceFile is null)
                {
                    continue;
                }

                var isExplicit = string.Equals(
                    sourceFile.GetMetadata(DiscoveryMetadataName),
                    ExplicitDiscoveryMetadataValue,
                    StringComparison.Ordinal);
                if (!isExplicit &&
                    (!configuration.IsAutomaticDetectionEnabled ||
                     configuration.BasePath is not null))
                {
                    continue;
                }

                var sourcePath = ResolvePath(
                    projectDirectory,
                    GetItemPath(sourceFile));
                if (isExplicit)
                {
                    _hotReloadDependencyFiles.Add(sourcePath);
                }

                if (File.Exists(sourcePath) &&
                    TryGetSupportedSourceKind(sourcePath, out _))
                {
                    resolvedSources.Add(sourcePath);
                }
            }

            if (configuration.BasePath is not null)
            {
                AddSourcesFromSpecifier(
                    entry.BaseDirectory,
                    configuration.BasePath,
                    resolvedSources);
            }

            foreach (var includedPath in configuration.IncludedPaths)
            {
                AddSourcesFromSpecifier(
                    entry.BaseDirectory,
                    includedPath,
                    resolvedSources);
            }

            var excludedSources = new HashSet<string>(
                GetPathComparer());
            foreach (var excludedPath in configuration.ExcludedPaths)
            {
                AddSourcesFromSpecifier(
                    entry.BaseDirectory,
                    excludedPath,
                    excludedSources);
            }

            resolvedSources.ExceptWith(excludedSources);
            sourceFiles = resolvedSources
                .OrderBy(path => path, GetPathComparer())
                .ToArray();
            return true;
        }
        catch (Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException or
                  ArgumentException or
                  NotSupportedException)
        {
            Log.LogErrorFromException(
                exception,
                showStackTrace: false);
            sourceFiles = Array.Empty<string>();
            return false;
        }
    }

    private void AddSourcesFromSpecifier(
        string baseDirectory,
        string specifier,
        ISet<string> paths)
    {
        _cancellation.Token.ThrowIfCancellationRequested();
        if (!ContainsWildcard(specifier))
        {
            var resolvedPath = ResolvePath(
                baseDirectory,
                specifier);
            if (File.Exists(resolvedPath))
            {
                if (TryGetSupportedSourceKind(resolvedPath, out _))
                {
                    paths.Add(resolvedPath);
                    _hotReloadDependencyFiles.Add(resolvedPath);
                }

                return;
            }

            if (!Directory.Exists(resolvedPath))
            {
                if (TryGetSupportedSourceKind(resolvedPath, out _))
                {
                    _hotReloadDependencyFiles.Add(resolvedPath);
                }
                else
                {
                    _hotReloadSourceDirectories.Add(resolvedPath);
                }

                Log.LogMessage(
                    MessageImportance.Low,
                    "ViuBundleUtilityCss: source path '{0}' does not exist.",
                    resolvedPath);
                return;
            }

            _hotReloadSourceDirectories.Add(resolvedPath);
            foreach (var path in Directory.EnumerateFiles(
                         resolvedPath,
                         "*",
                         SearchOption.AllDirectories))
            {
                _cancellation.Token.ThrowIfCancellationRequested();
                if (TryGetSupportedSourceKind(path, out _))
                {
                    paths.Add(Path.GetFullPath(path));
                }
            }

            return;
        }

        var wildcardPath = Path.IsPathRooted(specifier)
            ? specifier
            : Path.Combine(baseDirectory, specifier);
        var wildcardIndex = wildcardPath.IndexOfAny(
            new[] { '*', '?' });
        var separatorIndex = Math.Max(
            wildcardPath.LastIndexOf(
                Path.DirectorySeparatorChar,
                wildcardIndex),
            wildcardPath.LastIndexOf(
                Path.AltDirectorySeparatorChar,
                wildcardIndex));
        var rootText = separatorIndex >= 0
            ? wildcardPath.Substring(0, separatorIndex + 1)
            : baseDirectory;
        var rootDirectory = Path.GetFullPath(rootText);
        _hotReloadSourceDirectories.Add(rootDirectory);
        if (!Directory.Exists(rootDirectory))
        {
            Log.LogMessage(
                MessageImportance.Low,
                "ViuBundleUtilityCss: source glob root '{0}' does not exist.",
                rootDirectory);
            return;
        }

        var suffix = separatorIndex >= 0
            ? wildcardPath.Substring(separatorIndex + 1)
            : wildcardPath;
        var absolutePattern = Path.Combine(
            rootDirectory,
            suffix);
        var expression = CreateGlobExpression(absolutePattern);
        foreach (var path in Directory.EnumerateFiles(
                     rootDirectory,
                     "*",
                     SearchOption.AllDirectories))
        {
            _cancellation.Token.ThrowIfCancellationRequested();
            if (TryGetSupportedSourceKind(path, out _) &&
                expression.IsMatch(NormalizePath(path)))
            {
                paths.Add(Path.GetFullPath(path));
            }
        }
    }

    private static UtilityVariantRegistry CreateVariantRegistry(
        UtilityTheme theme,
        IEnumerable<UtilityCustomVariantDefinition> customVariants)
    {
        var definitions = theme.VariantRegistry.Definitions.ToList();
        var names = new HashSet<string>(
            definitions.Select(definition => definition.Name),
            StringComparer.Ordinal);
        foreach (var customVariant in customVariants)
        {
            if (names.Add(customVariant.Name))
            {
                definitions.Add(
                    new UtilityVariantDefinition(
                        customVariant.Name,
                        UtilityVariantKind.Static,
                        UtilityVariantCategory.State));
            }
        }

        return definitions.Count == theme.VariantRegistry.Definitions.Count
            ? theme.VariantRegistry
            : new UtilityVariantRegistry(definitions);
    }

    private static IReadOnlyList<UtilityClassMetadata> MergeRules(
        IEnumerable<UtilityClassMetadata> builtInRules,
        IEnumerable<UtilityClassMetadata> projectRules)
    {
        var rulesByCandidate =
            new Dictionary<string, UtilityClassMetadata>(
                StringComparer.Ordinal);
        foreach (var rule in builtInRules)
        {
            rulesByCandidate[rule.CandidateText] = rule;
        }

        foreach (var rule in projectRules)
        {
            rulesByCandidate[rule.CandidateText] = rule;
        }

        return rulesByCandidate.Values
            .OrderBy(rule => rule.SortOrder)
            .ThenBy(rule => rule.CandidateText, StringComparer.Ordinal)
            .ToArray();
    }

    private static string RenderUtilityLayer(
        IReadOnlyList<UtilityClassMetadata> rules)
    {
        if (rules.Count == 0)
        {
            return string.Empty;
        }

        var result = new StringBuilder();
        result.AppendLine("@layer utilities {");
        foreach (var rule in rules)
        {
            foreach (var line in NormalizeLineEndings(rule.Css).Split('\n'))
            {
                result.Append("  ");
                result.AppendLine(line);
            }
        }

        result.AppendLine("}");
        return result.ToString();
    }

    private static Regex CreateGlobExpression(string path)
    {
        var normalized = path
            .Replace(
                Path.DirectorySeparatorChar,
                '/')
            .Replace(
                Path.AltDirectorySeparatorChar,
                '/');
        var expression = new StringBuilder("^");
        for (var index = 0; index < normalized.Length; index++)
        {
            var character = normalized[index];
            if (character == '*')
            {
                var isRecursive =
                    index + 1 < normalized.Length &&
                    normalized[index + 1] == '*';
                if (isRecursive)
                {
                    index++;
                    if (index + 1 < normalized.Length &&
                        normalized[index + 1] == '/')
                    {
                        index++;
                        expression.Append("(?:.*/)?");
                    }
                    else
                    {
                        expression.Append(".*");
                    }
                }
                else
                {
                    expression.Append("[^/]*");
                }

                continue;
            }

            if (character == '?')
            {
                expression.Append("[^/]");
                continue;
            }

            expression.Append(
                Regex.Escape(character.ToString()));
        }

        expression.Append("$");
        var options = RegexOptions.CultureInvariant;
        if (Path.DirectorySeparatorChar == '\\')
        {
            options |= RegexOptions.IgnoreCase;
        }

        return new Regex(
            expression.ToString(),
            options);
    }

    private static bool ContainsWildcard(string path) =>
        path.IndexOfAny(new[] { '*', '?' }) >= 0;

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path)
            .Replace(
                Path.DirectorySeparatorChar,
                '/')
            .Replace(
                Path.AltDirectorySeparatorChar,
                '/');

    private static string ResolvePath(
        string baseDirectory,
        string path) =>
        Path.GetFullPath(
            Path.IsPathRooted(path)
                ? path
                : Path.Combine(baseDirectory, path));

    private static StringComparer GetPathComparer() =>
        Path.DirectorySeparatorChar == '\\'
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private static string NormalizeLineEndings(string value) =>
        value
            .Replace(
                "\r\n",
                "\n")
            .Replace(
                '\r',
                '\n');

    private static (int Line, int Column) GetLineAndColumn(
        string text,
        int offset)
    {
        var boundedOffset = Math.Max(
            0,
            Math.Min(offset, text.Length));
        var line = 1;
        var column = 1;
        for (var index = 0; index < boundedOffset; index++)
        {
            if (text[index] == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }

        return (line, column);
    }

    private static string GetItemPath(ITaskItem item)
    {
        var path = item.GetMetadata("FullPath");
        return string.IsNullOrEmpty(path)
            ? item.ItemSpec
            : path;
    }

    private bool DeleteObsoleteBundle()
    {
        try
        {
            if (PreserveEmptyBundleOnRemoval)
            {
                var directory = Path.GetDirectoryName(BundleOutputPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (!File.Exists(BundleOutputPath) ||
                    new FileInfo(BundleOutputPath).Length != 0)
                {
                    File.WriteAllText(
                        BundleOutputPath,
                        string.Empty,
                        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    BundleWritten = true;
                    Log.LogMessage(
                        MessageImportance.Normal,
                        "ViuBundleUtilityCss: ensured the empty utility hot-reload bundle at {0}.",
                        BundleOutputPath);
                }

                WriteEmptyBundleMarker();
                BundlePath = BundleOutputPath;
                BundleExists = true;
                return true;
            }

            if (File.Exists(BundleOutputPath))
            {
                File.Delete(BundleOutputPath);
                Log.LogMessage(
                    MessageImportance.Normal,
                    "ViuBundleUtilityCss: deleted obsolete utility bundle at {0}.",
                    BundleOutputPath);
            }
            else
            {
                Log.LogMessage(
                    MessageImportance.Low,
                    "ViuBundleUtilityCss: no resolvable utility candidates; no bundle written.");
            }

            DeleteEmptyBundleMarker();
            BundlePath = string.Empty;
            BundleExists = false;
            BundleWritten = false;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.LogErrorFromException(exception, showStackTrace: false);
            return false;
        }
    }

    private void WriteEmptyBundleMarker()
    {
        if (string.IsNullOrEmpty(EmptyBundleMarkerPath) ||
            File.Exists(EmptyBundleMarkerPath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(EmptyBundleMarkerPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(
            EmptyBundleMarkerPath,
            string.Empty,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void DeleteEmptyBundleMarker()
    {
        if (!string.IsNullOrEmpty(EmptyBundleMarkerPath) &&
            File.Exists(EmptyBundleMarkerPath))
        {
            File.Delete(EmptyBundleMarkerPath);
        }
    }

    private void WriteHotReloadDependencyManifest()
    {
        if (string.IsNullOrWhiteSpace(HotReloadDependencyManifestPath))
        {
            return;
        }

        try
        {
            var builder = new StringBuilder();
            builder.AppendLine("viu-css-hot-reload-dependencies-v1");
            foreach (var path in _hotReloadDependencyFiles.OrderBy(
                         value => value,
                         GetPathComparer()))
            {
                builder.Append("file:");
                builder.AppendLine(EncodeManifestPath(path));
            }

            foreach (var path in _hotReloadSourceDirectories.OrderBy(
                         value => value,
                         GetPathComparer()))
            {
                builder.Append("directory:");
                builder.AppendLine(EncodeManifestPath(path));
            }

            var text = builder.ToString();
            if (File.Exists(HotReloadDependencyManifestPath) &&
                string.Equals(
                    File.ReadAllText(HotReloadDependencyManifestPath),
                    text,
                    StringComparison.Ordinal))
            {
                return;
            }

            var directory = Path.GetDirectoryName(
                HotReloadDependencyManifestPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                HotReloadDependencyManifestPath,
                text,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch (Exception exception) when (
            exception is IOException or
                UnauthorizedAccessException or
                ArgumentException or
                NotSupportedException)
        {
            Log.LogWarningFromException(
                exception,
                showStackTrace: false);
        }
    }

    private static string EncodeManifestPath(string path) =>
        Convert.ToBase64String(
            Encoding.UTF8.GetBytes(
                Path.GetFullPath(path)));

    private static ScanRegion? GetScanRegion(
        string source,
        UtilitySourceKind sourceKind)
    {
        if (sourceKind == UtilitySourceKind.ViuComponent)
        {
            var template = SingleFileComponentParser.Parse(source).Descriptor.Template;
            return template is null
                ? null
                : new ScanRegion(
                    template.Content,
                    template.ContentLocation.Start.Offset);
        }

        if (sourceKind == UtilitySourceKind.VueComponent)
        {
            var template = VueSingleFileComponentParser.Parse(source).Descriptor.Template;
            return template is null
                ? null
                : new ScanRegion(
                    template.Content,
                    template.ContentLocation.Start.Offset);
        }

        return new ScanRegion(source, 0);
    }

    private static bool TryGetSupportedSourceKind(
        string sourcePath,
        out UtilitySourceKind sourceKind)
    {
        var extension = Path.GetExtension(sourcePath);
        if (string.Equals(extension, ".viu", StringComparison.OrdinalIgnoreCase))
        {
            sourceKind = UtilitySourceKind.ViuComponent;
            return true;
        }

        if (string.Equals(extension, ".vue", StringComparison.OrdinalIgnoreCase))
        {
            sourceKind = UtilitySourceKind.VueComponent;
            return true;
        }

        if (string.Equals(extension, ".html", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".htm", StringComparison.OrdinalIgnoreCase))
        {
            sourceKind = UtilitySourceKind.HostMarkup;
            return true;
        }

        sourceKind = default;
        return false;
    }

    private readonly struct UtilityStylesheetEntry
    {
        public UtilityStylesheetEntry(
            string sourceIdentity,
            string css,
            string baseDirectory)
        {
            SourceIdentity = sourceIdentity;
            Css = css;
            BaseDirectory = baseDirectory;
        }

        public string SourceIdentity { get; }

        public string Css { get; }

        public string BaseDirectory { get; }
    }

    private readonly struct CssSections
    {
        public CssSections(
            string prologue,
            string body)
        {
            Prologue = prologue;
            Body = body;
        }

        public string Prologue { get; }

        public string Body { get; }
    }

    private readonly struct ScanRegion
    {
        public ScanRegion(string text, int contentOffset)
        {
            Text = text;
            ContentOffset = contentOffset;
        }

        public string Text { get; }

        public int ContentOffset { get; }
    }

    private enum UtilitySourceKind
    {
        ViuComponent,
        VueComponent,
        HostMarkup,
    }
}
