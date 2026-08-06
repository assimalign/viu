using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.UtilityCss.Tests;

public sealed class UtilityCssConformanceTests
{
    private const string CompatibilityVersion = "4.3.3";

    [Fact]
    public void Manifest_V433Surface_ExactlyMatchesExecutableRegistriesAndDocumentedCatalog()
    {
        using var document = LoadJsonDocument(
            "compatibility-v4.3.3.json");
        var manifest = document.RootElement;

        manifest.GetProperty("schemaVersion").GetInt32().ShouldBe(1);
        manifest.GetProperty("compatibilityVersion").GetString()
            .ShouldBe(CompatibilityVersion);
        manifest.GetProperty("layerOrder").GetString()
            .ShouldBe(UtilityCssLayerEmitter.LayerOrder);
        ReadStringArray(
                manifest.GetProperty("compilerMetadata"))
            .ShouldBe(
                new[]
                {
                    "candidateText",
                    "description",
                    "css",
                    "sortOrder",
                });

        var officialReferences = manifest
            .GetProperty("officialReferences")
            .EnumerateObject()
            .ToArray();
        officialReferences.ShouldNotBeEmpty();
        officialReferences.ShouldAllBe(
            reference =>
                reference.Value.GetString()!
                    .StartsWith(
                        "https://",
                        StringComparison.Ordinal));
        manifest.GetProperty("officialReferences")
            .GetProperty("release")
            .GetString()!
            .ShouldContain("/tag/v4.3.3");
        manifest.GetProperty("officialReferences")
            .GetProperty("utilityRegistrations")
            .GetString()!
            .ShouldContain("/v4.3.3/");
        manifest.GetProperty("officialReferences")
            .GetProperty("variantRegistrations")
            .GetString()!
            .ShouldContain("/v4.3.3/");
        manifest.GetProperty("officialReferences")
            .GetProperty("defaultTheme")
            .GetString()!
            .ShouldContain("/v4.3.3/");
        officialReferences.ShouldAllBe(
            reference =>
                reference.Value.GetString()!
                    .IndexOf(
                        "/latest/",
                        StringComparison.OrdinalIgnoreCase) < 0);

        var manifestRoots = ReadStringArray(
                manifest.GetProperty("utilityRoots"))
            .OrderBy(root => root, StringComparer.Ordinal)
            .ToArray();
        var executableRoots = UtilityCssRegistry.BuiltIn.Definitions
            .Select(definition => definition.Root)
            .OrderBy(root => root, StringComparer.Ordinal)
            .ToArray();
        manifestRoots.ShouldBe(executableRoots);

        var manifestVariants = manifest
            .GetProperty("variants")
            .EnumerateArray()
            .Select(
                variant =>
                    variant.GetProperty("root").GetString() + "|" +
                    variant.GetProperty("kind").GetString() + "|" +
                    variant.GetProperty("category").GetString())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var executableVariants = UtilityVariantRegistry.BuiltIn.Definitions
            .Select(
                variant =>
                    variant.Name + "|" +
                    variant.Kind + "|" +
                    variant.Category)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        manifestVariants.ShouldBe(executableVariants);

        ReadStringArray(
                manifest.GetProperty("themeNamespaces"))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ShouldBe(
                UtilityTheme.Default.NamespaceNames
                    .OrderBy(value => value, StringComparer.Ordinal));
        ReadStringArray(
                manifest.GetProperty("directives"))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ShouldBe(
                Enum.GetNames(typeof(UtilityDirectiveKind))
                    .OrderBy(value => value, StringComparer.Ordinal));
        ReadStringArray(
                manifest.GetProperty("functions"))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ShouldBe(
                Enum.GetNames(typeof(UtilityCssFunctionKind))
                    .OrderBy(value => value, StringComparer.Ordinal));
        ReadStringArray(
                manifest.GetProperty("sourceForms"))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ShouldBe(
                new[]
                {
                    "import-source-none",
                    "import-source-path",
                    "source-brace-expansion",
                    "source-exclude-path",
                    "source-include-path",
                    "source-inline",
                    "source-not-inline",
                    "source-numeric-range",
                });

        var documentedFamilies = manifest
            .GetProperty("documentedUtilityFamilies");
        documentedFamilies.EnumerateObject()
            .Select(area => area.Name)
            .ShouldBe(
                new[]
                {
                    "Layout",
                    "FlexboxAndGrid",
                    "Spacing",
                    "Sizing",
                    "Typography",
                    "Backgrounds",
                    "Borders",
                    "Effects",
                    "Filters",
                    "Tables",
                    "TransitionsAndAnimation",
                    "Transforms",
                    "Interactivity",
                    "Svg",
                    "Accessibility",
                });
        var documentedFamilyNames = documentedFamilies
            .EnumerateObject()
            .SelectMany(area => ReadStringArray(area.Value))
            .ToArray();
        documentedFamilyNames.Length.ShouldBe(184);
        documentedFamilyNames
            .Distinct(StringComparer.Ordinal)
            .Count()
            .ShouldBe(documentedFamilyNames.Length);
        documentedFamilyNames.ShouldContain("block-size");
        documentedFamilyNames.ShouldContain("font-feature-settings");
        documentedFamilyNames.ShouldContain("inline-size");
        documentedFamilyNames.ShouldContain("scrollbar-color");
        documentedFamilyNames.ShouldContain("scrollbar-width");
        documentedFamilyNames.ShouldContain("tab-size");
        documentedFamilyNames.ShouldContain("zoom");
    }

    [Fact]
    public void Registry_EveryManifestRoot_HasExecutableCompilerAndEditorMetadata()
    {
        var completionItems = UtilityCssRegistry.BuiltIn.CompletionItems;
        foreach (var definition in UtilityCssRegistry.BuiltIn.Definitions)
        {
            definition.Description.ShouldNotBeNullOrWhiteSpace(
                $"Utility root '{definition.Root}' needs an editor description.");
            definition.Order.ShouldBeGreaterThanOrEqualTo(
                0,
                $"Utility root '{definition.Root}' needs a stable nonnegative order.");
            definition.CompletionCandidates.ShouldNotBeEmpty(
                $"Utility root '{definition.Root}' needs at least one complete candidate.");

            foreach (var completionCandidate in definition.CompletionCandidates)
            {
                var resolution = UtilityCssRegistry.BuiltIn.Resolve(
                    completionCandidate);
                resolution.IsSuccess.ShouldBeTrue(
                    $"Completion '{completionCandidate}' for root '{definition.Root}' must compile.");
                resolution.Diagnostics.ShouldBeEmpty(
                    $"Completion '{completionCandidate}' for root '{definition.Root}' must be diagnostic-free.");
                var metadata = resolution.Metadata;
                metadata.ShouldNotBeNull();
                metadata.CandidateText.ShouldBe(completionCandidate);
                metadata.Description.ShouldNotBeNullOrWhiteSpace();
                metadata.Css.ShouldNotBeNullOrWhiteSpace();
                metadata.SortOrder.ShouldBeGreaterThanOrEqualTo(0);
                completionItems.ShouldContain(
                    completion =>
                        completion.CandidateText == metadata.CandidateText &&
                        completion.Description == metadata.Description &&
                        completion.Css == metadata.Css &&
                        completion.SortOrder == metadata.SortOrder,
                    $"Completion '{completionCandidate}' must use the compiler's exact metadata.");
            }
        }
    }

    [Fact]
    public void GoldenVectors_CoverEveryManifestPromise_AndExecuteWithoutTailwind()
    {
        using var manifestDocument = LoadJsonDocument(
            "compatibility-v4.3.3.json");
        using var vectorsDocument = LoadJsonDocument(
            manifestDocument.RootElement
                .GetProperty("goldenVectorFile")
                .GetString()!);
        var manifest = manifestDocument.RootElement;
        var vectorRoot = vectorsDocument.RootElement;

        vectorRoot.GetProperty("schemaVersion").GetInt32().ShouldBe(1);
        vectorRoot.GetProperty("compatibilityVersion").GetString()
            .ShouldBe(CompatibilityVersion);
        var vectors = vectorRoot.GetProperty("vectors")
            .EnumerateArray()
            .ToArray();
        vectors.ShouldNotBeEmpty();
        var identifiers = vectors
            .Select(vector => vector.GetProperty("id").GetString()!)
            .ToArray();
        identifiers.ShouldAllBe(identifier => !string.IsNullOrWhiteSpace(identifier));
        identifiers.Distinct(StringComparer.Ordinal).Count()
            .ShouldBe(identifiers.Length);
        vectors.ShouldAllBe(
            vector =>
                vector.GetProperty("officialReference")
                    .GetString()!
                    .StartsWith(
                        "https://",
                        StringComparison.Ordinal));

        var coveredModes = vectors
            .Where(vector => vector.TryGetProperty("modes", out _))
            .SelectMany(
                vector => ReadStringArray(
                    vector.GetProperty("modes")))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        ReadStringArray(manifest.GetProperty("promisedModes"))
            .Except(coveredModes, StringComparer.Ordinal)
            .ShouldBeEmpty();

        var coveredSourceForms = vectors
            .Where(vector => vector.TryGetProperty("forms", out _))
            .SelectMany(
                vector => ReadStringArray(
                    vector.GetProperty("forms")))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        coveredSourceForms.ShouldBe(
            ReadStringArray(manifest.GetProperty("sourceForms"))
                .OrderBy(value => value, StringComparer.Ordinal));

        var coveredDirectives = vectors
            .Where(vector => vector.TryGetProperty("directives", out _))
            .SelectMany(
                vector => ReadStringArray(
                    vector.GetProperty("directives")))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        coveredDirectives.ShouldBe(
            ReadStringArray(manifest.GetProperty("directives"))
                .OrderBy(value => value, StringComparer.Ordinal));

        var coveredFunctions = vectors
            .Where(vector => vector.TryGetProperty("functions", out _))
            .SelectMany(
                vector => ReadStringArray(
                    vector.GetProperty("functions")))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        coveredFunctions.ShouldBe(
            ReadStringArray(manifest.GetProperty("functions"))
                .OrderBy(value => value, StringComparer.Ordinal));

        foreach (var vector in vectors)
        {
            ExecuteGoldenVector(vector);
        }
    }

    [Fact]
    public void ConformanceJson_IsTestDataAndIsNotEmbeddedOrLoadedByShippingLibrary()
    {
        typeof(UtilityCssRegistry).Assembly
            .GetManifestResourceNames()
            .ShouldAllBe(
                resourceName =>
                    !resourceName.EndsWith(
                        ".json",
                        StringComparison.OrdinalIgnoreCase));

        var utilityLibraryRoot = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                ".."));
        var shippingProjectPath = Path.Combine(
            utilityLibraryRoot,
            "src",
            "Assimalign.Viu.UtilityCss.csproj");
        File.Exists(shippingProjectPath).ShouldBeTrue();
        File.ReadAllText(shippingProjectPath)
            .ShouldNotContain(
                "conformance",
                Case.Insensitive);
    }

    private static void ExecuteGoldenVector(JsonElement vector)
    {
        var identifier = vector.GetProperty("id").GetString()!;
        switch (vector.GetProperty("kind").GetString())
        {
            case "compiler":
                ExecuteCompilerVector(identifier, vector);
                break;
            case "layer":
                ExecuteLayerVector(identifier, vector);
                break;
            case "source":
                ExecuteSourceVector(identifier, vector);
                break;
            case "stylesheet":
                ExecuteStylesheetVector(identifier, vector);
                break;
            case "theme":
                ExecuteThemeVector(identifier, vector);
                break;
            default:
                throw new InvalidOperationException(
                    $"Golden vector '{identifier}' has an unknown kind.");
        }
    }

    private static void ExecuteCompilerVector(
        string identifier,
        JsonElement vector)
    {
        var result = UtilityCssCompiler.Compile(
            ReadStringArray(vector.GetProperty("candidates")));
        result.Diagnostics.ShouldBeEmpty(
            $"Golden vector '{identifier}' produced diagnostics.");
        var normalizedCss = NormalizeLineEndings(result.Css);

        if (vector.TryGetProperty(
                "expectedCssExact",
                out var expectedCssExact))
        {
            normalizedCss.ShouldBe(
                expectedCssExact.GetString(),
                $"Golden vector '{identifier}' changed canonical CSS ordering.");
        }

        AssertFragments(
            identifier,
            normalizedCss,
            vector,
            "expectedCssFragments",
            shouldContain: true);
        AssertOrderedFragments(
            identifier,
            normalizedCss,
            vector,
            "expectedCssOrder");

        if (vector.TryGetProperty(
                "expectedMetadataCandidates",
                out var expectedMetadataCandidates))
        {
            result.Rules
                .Select(rule => rule.CandidateText)
                .ShouldBe(ReadStringArray(expectedMetadataCandidates));
        }

        if (vector.TryGetProperty(
                "expectedRejectedCandidates",
                out var expectedRejectedCandidates))
        {
            foreach (var candidate in ReadStringArray(
                         expectedRejectedCandidates))
            {
                var rejected = UtilityCssCompiler.Compile(
                    new[] { candidate });
                rejected.Rules.ShouldBeEmpty(
                    $"Golden vector '{identifier}' unexpectedly compiled '{candidate}'.");
                rejected.Diagnostics.ShouldNotBeEmpty(
                    $"Golden vector '{identifier}' must diagnose rejected candidate '{candidate}'.");
            }
        }
    }

    private static void ExecuteLayerVector(
        string identifier,
        JsonElement vector)
    {
        var expectedLayerOrder = vector
            .GetProperty("expectedLayerOrder")
            .GetString()!;
        expectedLayerOrder.ShouldBe(UtilityCssLayerEmitter.LayerOrder);
        var css = NormalizeLineEndings(
            UtilityCssLayerEmitter.EmitDesignSystem(
                UtilityTheme.Default,
                CancellationToken.None));
        css.ShouldStartWith(expectedLayerOrder);
        AssertFragments(
            identifier,
            css,
            vector,
            "expectedCssFragments",
            shouldContain: true);
        AssertOrderedFragments(
            identifier,
            css,
            vector,
            "expectedCssOrder");
    }

    private static void ExecuteSourceVector(
        string identifier,
        JsonElement vector)
    {
        var result = UtilitySourceParser.Parse(
            vector.GetProperty("stylesheet").GetString());
        result.Diagnostics.ShouldBeEmpty(
            $"Golden vector '{identifier}' produced source diagnostics.");
        result.Configuration.HasUtilitiesImport.ShouldBeTrue();

        if (vector.TryGetProperty(
                "expectedBasePath",
                out var expectedBasePath))
        {
            result.Configuration.BasePath.ShouldBe(
                expectedBasePath.GetString());
        }

        if (vector.TryGetProperty(
                "expectedAutomaticDetection",
                out var expectedAutomaticDetection))
        {
            result.Configuration.IsAutomaticDetectionEnabled.ShouldBe(
                expectedAutomaticDetection.GetBoolean());
        }

        AssertStringCollection(
            result.Configuration.IncludedPaths,
            vector,
            "expectedIncludedPaths");
        AssertStringCollection(
            result.Configuration.ExcludedPaths,
            vector,
            "expectedExcludedPaths");
        AssertStringCollection(
            result.Configuration.IncludedCandidates,
            vector,
            "expectedIncludedCandidates");
        AssertStringCollection(
            result.Configuration.ExcludedCandidates,
            vector,
            "expectedExcludedCandidates");
    }

    private static void ExecuteStylesheetVector(
        string identifier,
        JsonElement vector)
    {
        var result = UtilityStylesheetParser.Parse(
            vector.GetProperty("stylesheet").GetString());
        result.Diagnostics.ShouldBeEmpty(
            $"Golden vector '{identifier}' produced stylesheet diagnostics.");
        result.Directives
            .Select(directive => directive.Kind.ToString())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ShouldBe(
                ReadStringArray(vector.GetProperty("directives"))
                    .OrderBy(value => value, StringComparer.Ordinal));
        result.Functions
            .Select(function => function.Kind.ToString())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ShouldBe(
                ReadStringArray(vector.GetProperty("functions"))
                    .OrderBy(value => value, StringComparer.Ordinal));
        result.Directives.ShouldAllBe(directive => directive.IsValid);
        result.Functions.ShouldAllBe(function => function.IsValid);
    }

    private static void ExecuteThemeVector(
        string identifier,
        JsonElement vector)
    {
        var parseOptions = new UtilityThemeParseOptions
        {
            Prefix = vector.TryGetProperty(
                "parsePrefix",
                out var parsePrefix)
                    ? parsePrefix.GetString()
                    : null,
            IsImportant = vector.TryGetProperty(
                "parseImportant",
                out var parseImportant) &&
                parseImportant.GetBoolean(),
        };
        var parsedTheme = UtilityThemeParser.Parse(
            vector.GetProperty("stylesheet").GetString(),
            parseOptions,
            CancellationToken.None);
        parsedTheme.Diagnostics.ShouldBeEmpty(
            $"Golden vector '{identifier}' produced theme diagnostics.");

        var compilation = UtilityCssCompiler.Compile(
            ReadStringArray(vector.GetProperty("candidates")),
            UtilityCssRegistry.BuiltIn,
            parsedTheme.Theme,
            CancellationToken.None);
        compilation.Diagnostics.ShouldBeEmpty(
            $"Golden vector '{identifier}' produced compiler diagnostics.");
        var normalizedCompilationCss = NormalizeLineEndings(
            compilation.Css);
        AssertFragments(
            identifier,
            normalizedCompilationCss,
            vector,
            "expectedCompilationFragments",
            shouldContain: true);

        var emissionOptions = Enum.Parse<UtilityThemeOptions>(
            vector.GetProperty("emissionOptions").GetString()!);
        var emittedCss = NormalizeLineEndings(
            UtilityCssLayerEmitter.EmitDesignSystem(
                parsedTheme.Theme,
                compilation.Css,
                emissionOptions,
                CancellationToken.None));
        AssertFragments(
            identifier,
            emittedCss,
            vector,
            "expectedEmissionFragments",
            shouldContain: true);
        AssertFragments(
            identifier,
            emittedCss,
            vector,
            "unexpectedEmissionFragments",
            shouldContain: false);
    }

    private static void AssertFragments(
        string identifier,
        string css,
        JsonElement vector,
        string propertyName,
        bool shouldContain)
    {
        if (!vector.TryGetProperty(
                propertyName,
                out var fragments))
        {
            return;
        }

        foreach (var fragment in ReadStringArray(fragments))
        {
            if (shouldContain)
            {
                css.ShouldContain(
                    fragment,
                    customMessage:
                        $"Golden vector '{identifier}' is missing '{fragment}'.");
            }
            else
            {
                css.ShouldNotContain(
                    fragment,
                    customMessage:
                        $"Golden vector '{identifier}' unexpectedly emitted '{fragment}'.");
            }
        }
    }

    private static void AssertOrderedFragments(
        string identifier,
        string css,
        JsonElement vector,
        string propertyName)
    {
        if (!vector.TryGetProperty(
                propertyName,
                out var fragments))
        {
            return;
        }

        var previousIndex = -1;
        foreach (var fragment in ReadStringArray(fragments))
        {
            var index = css.IndexOf(
                fragment,
                StringComparison.Ordinal);
            index.ShouldBeGreaterThan(
                previousIndex,
                $"Golden vector '{identifier}' changed the order of '{fragment}'.");
            previousIndex = index;
        }
    }

    private static void AssertStringCollection(
        IEnumerable<string> actual,
        JsonElement vector,
        string propertyName)
    {
        if (vector.TryGetProperty(
                propertyName,
                out var expected))
        {
            actual.ShouldBe(ReadStringArray(expected));
        }
    }

    private static JsonDocument LoadJsonDocument(string fileName)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "conformance",
            fileName);
        File.Exists(path).ShouldBeTrue(
            $"Conformance test data '{fileName}' was not copied to the test output.");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string[] ReadStringArray(JsonElement element) =>
        element.EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
}
