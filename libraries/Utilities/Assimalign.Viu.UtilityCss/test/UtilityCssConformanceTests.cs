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
    private const string UtilitySourceReference =
        "https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/utilities.ts";

    [Fact]
    public void Manifest_V433Surface_ExactlyMatchesExecutableRegistriesAndDocumentedCatalog()
    {
        using var document = LoadJsonDocument(
            "compatibility-v4.3.3.json");
        var manifest = document.RootElement;

        manifest.GetProperty("schemaVersion").GetInt32().ShouldBe(3);
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
            .SelectMany(
                area => area.Value
                    .EnumerateArray()
                    .Select(
                        family => family.GetProperty("name").GetString()!))
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
        // [V01.01.12.16.02] pins independently authored emitted-CSS coverage
        // for every documented v4.3.3 utility family and its declared modes.
        using var manifestDocument = LoadJsonDocument(
            "compatibility-v4.3.3.json");
        using var vectorsDocument = LoadJsonDocument(
            manifestDocument.RootElement
                .GetProperty("goldenVectorFile")
                .GetString()!);
        var manifest = manifestDocument.RootElement;
        var vectorRoot = vectorsDocument.RootElement;

        vectorRoot.GetProperty("schemaVersion").GetInt32().ShouldBe(3);
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

        var familyVectors = vectorRoot.GetProperty("familyVectors")
            .EnumerateArray()
            .ToArray();
        AssertFamilyVectors(manifest, familyVectors);

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

    private static void AssertFamilyVectors(
        JsonElement manifest,
        JsonElement[] familyVectors)
    {
        var familyContracts = manifest
            .GetProperty("documentedUtilityFamilies")
            .EnumerateObject()
            .SelectMany(
                area => area.Value
                    .EnumerateArray()
                    .Select(
                        family => new
                        {
                            Location = area.Name + "|" +
                                family.GetProperty("name").GetString(),
                            Contract = family,
                        }))
            .ToArray();
        var expectedLocations = familyContracts
            .Select(contract => contract.Location)
            .ToArray();
        var actualLocations = familyVectors
            .Select(
                vector =>
                    vector.GetProperty("area").GetString() + "|" +
                    vector.GetProperty("family").GetString())
            .ToArray();
        actualLocations.ShouldBe(expectedLocations);
        actualLocations.Distinct(StringComparer.Ordinal).Count()
            .ShouldBe(actualLocations.Length);

        var identifiers = familyVectors
            .Select(vector => vector.GetProperty("id").GetString()!)
            .ToArray();
        identifiers.ShouldAllBe(identifier => !string.IsNullOrWhiteSpace(identifier));
        identifiers.Distinct(StringComparer.Ordinal).Count()
            .ShouldBe(identifiers.Length);

        HashSet<string> promisedModes = new(
            ReadStringArray(manifest.GetProperty("promisedModes")),
            StringComparer.Ordinal);
        var prefixedTheme = UtilityThemeParser.Parse(
            string.Empty,
            new UtilityThemeParseOptions { Prefix = "vu" },
            CancellationToken.None);
        prefixedTheme.Diagnostics.ShouldBeEmpty();
        List<string> executionFailures = new();

        foreach (var familyVector in familyVectors)
        {
            var identifier = familyVector.GetProperty("id").GetString()!;
            var area = familyVector.GetProperty("area").GetString()!;
            var family = familyVector.GetProperty("family").GetString()!;
            var familyContract = familyContracts
                .Single(contract => contract.Location == area + "|" + family)
                .Contract;
            var officialReference = familyContract
                .GetProperty("officialReference")
                .GetString();
            officialReference.ShouldBe(
                "https://tailwindcss.com/docs/" + family,
                $"Family contract '{identifier}' must cite the narrowest official family reference.");
            familyVector.GetProperty("officialReference")
                .GetString()
                .ShouldBe(
                    officialReference,
                    $"Family vector '{identifier}' must cite its manifest contract.");
            familyContract.GetProperty("sourceReference")
                .GetString()
                .ShouldBe(UtilitySourceReference);
            familyVector.GetProperty("sourceReference")
                .GetString()
                .ShouldBe(
                    UtilitySourceReference,
                    $"Family vector '{identifier}' must pin the official v4.3.3 utility registrations.");

            var roots = ReadStringArray(familyVector.GetProperty("roots"));
            roots.ShouldBe(ReadStringArray(familyContract.GetProperty("roots")));

            var supportsNegativeValues = false;
            foreach (var root in roots)
            {
                UtilityCssRegistry.BuiltIn.TryGetDefinition(
                        root,
                        out UtilityDefinition? definition)
                    .ShouldBeTrue(
                        $"Family vector '{identifier}' names unregistered root '{root}'.");
                definition.ShouldNotBeNull();
                supportsNegativeValues |= definition.SupportsNegativeValues;
            }

            var cases = familyVector.GetProperty("cases")
                .EnumerateArray()
                .ToArray();
            cases.ShouldNotBeEmpty(
                $"Family vector '{identifier}' must assert emitted CSS.");
            var modes = cases
                .Select(@case => @case.GetProperty("mode").GetString()!)
                .ToArray();
            modes.ShouldBe(
                ReadStringArray(familyContract.GetProperty("modes")),
                $"Family vector '{identifier}' must cover every mode its manifest contract declares.");
            modes.Distinct(StringComparer.Ordinal).Count()
                .ShouldBe(
                    modes.Length,
                    $"Family vector '{identifier}' repeats a mode instead of adding a distinct case.");
            modes.ShouldAllBe(
                mode => promisedModes.Contains(mode),
                $"Family vector '{identifier}' declares a mode outside the v4.3.3 contract.");
            modes.ShouldContain("important");
            modes.ShouldContain("compound-variant");
            modes.ShouldContain("prefix");
            modes.Any(
                    mode => mode == "static"
                        || mode == "named-value"
                        || mode == "bare-value"
                        || mode == "fraction"
                        || mode == "arbitrary-value")
                .ShouldBeTrue(
                    $"Family vector '{identifier}' has no value-emission case.");
            if (supportsNegativeValues)
            {
                modes.ShouldContain(
                    "negative",
                    $"Family vector '{identifier}' omits its registered negative mode.");
            }

            var declarationContracts = familyVector
                .GetProperty("declarationContracts")
                .EnumerateObject()
                .ToDictionary(
                    property => property.Name,
                    property => property.Value,
                    StringComparer.Ordinal);
            declarationContracts.ShouldNotBeEmpty(
                $"Family vector '{identifier}' must pin complete declarations.");
            foreach (var declarationContract in declarationContracts)
            {
                var declarations = ReadDeclarationContract(
                    declarationContract.Value);
                declarations.ShouldNotBeEmpty(
                    $"Family vector '{identifier}' declaration contract '{declarationContract.Key}' is empty.");
                declarations.ShouldAllBe(
                    declaration => declaration.Value.Length != 0,
                    $"Family vector '{identifier}' declaration contract '{declarationContract.Key}' contains an empty value.");
                declarations
                    .Select(declaration => declaration.Property)
                    .Distinct(StringComparer.Ordinal)
                    .Count()
                    .ShouldBe(
                        declarations.Length,
                        $"Family vector '{identifier}' declaration contract '{declarationContract.Key}' repeats a property.");
            }

            var canonicalMode = cases[0].GetProperty("mode").GetString()!;
            canonicalMode.ShouldNotBe("important");
            canonicalMode.ShouldNotBe("compound-variant");
            canonicalMode.ShouldNotBe("prefix");
            var usedDeclarationContracts = new HashSet<string>(StringComparer.Ordinal);
            foreach (var familyCase in cases)
            {
                familyCase.TryGetProperty("expectedCssFragments", out _)
                    .ShouldBeFalse(
                        $"Family vector '{identifier}' must not use subset CSS fragments.");
                var mode = familyCase.GetProperty("mode").GetString()!;
                familyCase.GetProperty("important")
                    .GetBoolean()
                    .ShouldBe(
                        mode == "important",
                        $"Family vector '{identifier}' mode '{mode}' must declare importance exactly.");
                var rulePaths = familyCase.GetProperty("expectedRulePaths")
                    .EnumerateArray()
                    .ToArray();
                rulePaths.ShouldNotBeEmpty();
                foreach (var rulePath in rulePaths)
                {
                    var declarationContract = rulePath
                        .GetProperty("declarationContract")
                        .GetString()!;
                    declarationContracts.ContainsKey(declarationContract)
                        .ShouldBeTrue(
                            $"Family vector '{identifier}' mode '{mode}' names missing declaration contract '{declarationContract}'.");
                    usedDeclarationContracts.Add(declarationContract);
                }

                var caseContract = rulePaths[0]
                    .GetProperty("declarationContract")
                    .GetString()!;
                rulePaths.ShouldAllBe(
                    rulePath => rulePath.GetProperty("declarationContract").GetString() == caseContract,
                    $"Family vector '{identifier}' mode '{mode}' must use one complete declaration contract for every emitted rule path.");
                if (mode is "important" or "compound-variant")
                {
                    caseContract.ShouldBe(
                        canonicalMode,
                        $"Family vector '{identifier}' mode '{mode}' must reuse its canonical declarations.");
                }
                else if (mode == "prefix")
                {
                    (caseContract == canonicalMode || caseContract == "prefix")
                        .ShouldBeTrue(
                            $"Family vector '{identifier}' prefix mode must use canonical declarations or an exact prefixed contract.");
                }
                else
                {
                    caseContract.ShouldBe(
                        mode,
                        $"Family vector '{identifier}' mode '{mode}' needs its own exact declaration contract.");
                }
            }

            usedDeclarationContracts
                .OrderBy(value => value, StringComparer.Ordinal)
                .ShouldBe(
                    declarationContracts.Keys.OrderBy(value => value, StringComparer.Ordinal),
                    $"Family vector '{identifier}' contains an unexercised declaration contract.");

            foreach (var familyCase in cases)
            {
                try
                {
                    ExecuteFamilyVectorCase(
                        identifier,
                        familyCase,
                        declarationContracts,
                        prefixedTheme.Theme);
                }
                catch (ShouldAssertException exception)
                {
                    executionFailures.Add(identifier + ": " + exception.Message);
                }
            }
        }

        executionFailures.ShouldBeEmpty(
            "Every independently authored family vector must compile and match its CSS contract.");
    }

    private static void ExecuteFamilyVectorCase(
        string identifier,
        JsonElement familyCase,
        IReadOnlyDictionary<string, JsonElement> declarationContracts,
        UtilityTheme prefixedTheme)
    {
        var mode = familyCase.GetProperty("mode").GetString()!;
        var candidate = familyCase.GetProperty("candidate").GetString()!;
        switch (mode)
        {
            case "important":
                candidate.ShouldEndWith("!");
                break;
            case "compound-variant":
                candidate.ShouldStartWith("group-hover:");
                break;
            case "prefix":
                candidate.ShouldStartWith("vu:");
                break;
            case "negative":
                candidate.ShouldStartWith("-");
                break;
            case "fraction":
            case "modifier":
                candidate.ShouldContain("/");
                break;
            case "arbitrary-value":
                candidate.ShouldContain("[");
                break;
            case "css-variable":
                candidate.ShouldContain("--viu-family-value");
                break;
        }

        UtilityTheme theme = mode == "prefix"
            ? prefixedTheme
            : UtilityTheme.Default;
        var first = UtilityCssCompiler.Compile(
            new[] { candidate },
            UtilityCssRegistry.BuiltIn,
            theme,
            CancellationToken.None);
        first.Diagnostics.ShouldBeEmpty(
            $"Family vector '{identifier}' mode '{mode}' rejected '{candidate}'.");
        var emittedRule = first.Rules.ShouldHaveSingleItem(
            $"Family vector '{identifier}' mode '{mode}' must emit exactly one metadata rule for '{candidate}'.");
        var actualSemanticRules = ParseSemanticRules(
            emittedRule.Css,
            identifier,
            mode);
        var isImportant = familyCase.GetProperty("important").GetBoolean();
        var expectedSemanticRules = familyCase
            .GetProperty("expectedRulePaths")
            .EnumerateArray()
            .Select(
                rulePath => DescribeSemanticRule(
                    ReadStringArray(rulePath.GetProperty("wrappers")),
                    rulePath.GetProperty("selector").GetString()!,
                    ReadDeclarationContract(
                        declarationContracts[
                            rulePath.GetProperty("declarationContract").GetString()!]),
                    isImportant))
            .ToArray();
        actualSemanticRules.ShouldBe(
            expectedSemanticRules,
            $"Family vector '{identifier}' mode '{mode}' changed its complete selector/declaration semantics.");

        var second = UtilityCssCompiler.Compile(
            new[] { candidate },
            UtilityCssRegistry.BuiltIn,
            theme,
            CancellationToken.None);
        second.Css.ShouldBe(
            first.Css,
            $"Family vector '{identifier}' mode '{mode}' is not byte deterministic.");
    }

    private static DeclarationContract[] ReadDeclarationContract(
        JsonElement declarationContract) =>
        declarationContract
            .EnumerateArray()
            .Select(
                declaration => new DeclarationContract(
                    declaration.GetProperty("property").GetString()!,
                    declaration.GetProperty("value").GetString()!))
            .ToArray();

    private static string[] ParseSemanticRules(
        string css,
        string identifier,
        string mode)
    {
        var wrappers = new List<string>();
        var rules = new List<string>();
        string? selector = null;
        List<DeclarationContract>? declarations = null;
        List<bool>? importance = null;

        foreach (var rawLine in NormalizeLineEndings(css).Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (selector is null)
            {
                if (line == "}")
                {
                    if (wrappers.Count == 0)
                    {
                        throw InvalidSemanticCss(identifier, mode, line);
                    }

                    wrappers.RemoveAt(wrappers.Count - 1);
                    continue;
                }

                if (!line.EndsWith(" {", StringComparison.Ordinal))
                {
                    throw InvalidSemanticCss(identifier, mode, line);
                }

                var header = line.Substring(0, line.Length - 2);
                if (header.StartsWith("@", StringComparison.Ordinal))
                {
                    wrappers.Add(header);
                    continue;
                }

                selector = header;
                declarations = new List<DeclarationContract>();
                importance = new List<bool>();
                continue;
            }

            if (line == "}")
            {
                rules.Add(
                    DescribeSemanticRule(
                        wrappers,
                        selector,
                        declarations!,
                        importance!));
                selector = null;
                declarations = null;
                importance = null;
                continue;
            }

            if (!line.EndsWith(";", StringComparison.Ordinal))
            {
                throw InvalidSemanticCss(identifier, mode, line);
            }

            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                throw InvalidSemanticCss(identifier, mode, line);
            }

            var property = line.Substring(0, separator);
            var value = line.Substring(separator + 1, line.Length - separator - 2).TrimStart();
            var isImportant = value.EndsWith(
                " !important",
                StringComparison.Ordinal);
            if (isImportant)
            {
                value = value.Substring(0, value.Length - " !important".Length);
            }

            declarations!.Add(new DeclarationContract(property, value));
            importance!.Add(isImportant);
        }

        if (selector is not null || wrappers.Count != 0)
        {
            throw InvalidSemanticCss(identifier, mode, "end of CSS");
        }

        return rules.ToArray();
    }

    private static InvalidOperationException InvalidSemanticCss(
        string identifier,
        string mode,
        string line) =>
        new(
            $"Family vector '{identifier}' mode '{mode}' emitted CSS outside the conformance rule grammar at '{line}'.");

    private static string DescribeSemanticRule(
        IReadOnlyList<string> wrappers,
        string selector,
        IReadOnlyList<DeclarationContract> declarations,
        bool isImportant) =>
        DescribeSemanticRule(
            wrappers,
            selector,
            declarations,
            Enumerable.Repeat(isImportant, declarations.Count).ToArray());

    private static string DescribeSemanticRule(
        IReadOnlyList<string> wrappers,
        string selector,
        IReadOnlyList<DeclarationContract> declarations,
        IReadOnlyList<bool> importance)
    {
        declarations.Count.ShouldBe(importance.Count);
        return string.Join("\u001f", wrappers) +
            "\u001e" + selector +
            "\u001e" + string.Join(
                "\u001f",
                declarations.Select(
                    (declaration, index) =>
                        declaration.Property + "\u001d" +
                        declaration.Value + "\u001d" +
                        importance[index]));
    }

    private sealed record DeclarationContract(
        string Property,
        string Value);

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
