using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// The immutable executable registry shared by utility CSS generation, completion, and hover.
/// The built-in registry implements the v4.3.3 utility roots frozen by the repository-owned
/// conformance manifest tracked by <c>[V01.01.12.16]</c>. The compatibility target is the documented
/// <see href="https://tailwindcss.com/docs/styling-with-utility-classes">
/// Tailwind CSS v4 utility-class model</see>; the implementation and registry data are Viu-owned.
/// </summary>
public sealed class UtilityCssRegistry
{
    private readonly Dictionary<string, UtilityRegisteredDefinition> definitionsByRoot;

    private UtilityCssRegistry(
        IEnumerable<UtilityRegisteredDefinition> registrations)
    {
        var orderedRegistrations = registrations
            .OrderBy(
                registration => registration.Definition.Order)
            .ThenBy(
                registration => registration.Definition.Root,
                StringComparer.Ordinal)
            .ToArray();

        definitionsByRoot = new Dictionary<string, UtilityRegisteredDefinition>(
            orderedRegistrations.Length,
            StringComparer.Ordinal);
        foreach (var registration in orderedRegistrations)
        {
            definitionsByRoot.Add(
                registration.Definition.Root,
                registration);
        }

        Definitions = new UtilityCollection<UtilityDefinition>(
            orderedRegistrations.Select(registration => registration.Definition));

        CompletionItems = CreateCompletionItems(UtilityTheme.Default);
    }

    /// <summary>
    /// Gets the built-in executable v4.3.3 compatibility registry.
    /// </summary>
    public static UtilityCssRegistry BuiltIn { get; } =
        new(CreateBuiltInRegistrations());

    /// <summary>
    /// Gets the compatibility contract pinned by this executable catalog.
    /// </summary>
    public static string CompatibilityContract =>
        "Viu-owned standalone Tailwind CSS v4.3.3 compatibility manifest";

    /// <summary>
    /// Gets utility-family definitions in stable property order.
    /// </summary>
    public UtilityCollection<UtilityDefinition> Definitions { get; }

    /// <summary>
    /// Gets representative completion items resolved from this registry and the default theme.
    /// The CSS preview and hover description therefore cannot drift from compiler output.
    /// </summary>
    public UtilityCollection<UtilityClassMetadata> CompletionItems { get; }

    /// <summary>
    /// Looks up public metadata for an exact parsed utility root.
    /// </summary>
    /// <param name="root">The parsed root.</param>
    /// <param name="definition">The matching immutable definition when found.</param>
    /// <returns><see langword="true"/> when the root is implemented.</returns>
    public bool TryGetDefinition(
        string root,
        out UtilityDefinition? definition)
    {
        if (definitionsByRoot.TryGetValue(root, out var registration))
        {
            definition = registration.Definition;
            return true;
        }

        definition = null;
        return false;
    }

    /// <summary>
    /// Resolves one class candidate with the built-in default theme.
    /// </summary>
    /// <param name="candidateText">The complete class candidate.</param>
    /// <returns>Compiler and editor metadata, or recoverable diagnostics.</returns>
    public UtilityClassResolutionResult Resolve(string candidateText) =>
        Resolve(
            candidateText,
            UtilityTheme.Default,
            CancellationToken.None);

    /// <summary>
    /// Resolves one class candidate with an explicit theme and cancellation boundary.
    /// </summary>
    /// <param name="candidateText">The complete class candidate.</param>
    /// <param name="theme">The immutable theme to use.</param>
    /// <param name="cancellationToken">The build or editor cancellation boundary.</param>
    /// <returns>Compiler and editor metadata, or recoverable diagnostics.</returns>
    /// <exception cref="System.OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    public UtilityClassResolutionResult Resolve(
        string candidateText,
        UtilityTheme theme,
        CancellationToken cancellationToken) =>
        UtilityResolutionEngine.Resolve(
            this,
            theme ?? UtilityTheme.Default,
            candidateText,
            cancellationToken);

    /// <summary>
    /// Gets default-theme completion items whose complete candidate begins with
    /// <paramref name="prefix"/>.
    /// </summary>
    /// <param name="prefix">The optional ordinal candidate prefix.</param>
    /// <returns>Matching items in deterministic compiler order.</returns>
    public UtilityCollection<UtilityClassMetadata> GetCompletions(string? prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return CompletionItems;
        }

        return new UtilityCollection<UtilityClassMetadata>(
            CompletionItems.Where(
                item => item.CandidateText.StartsWith(
                    prefix,
                    StringComparison.Ordinal)));
    }

    /// <summary>
    /// Gets completion items resolved against an explicit CSS-first theme.
    /// Theme-backed color, spacing, sizing, font, radius, and breakpoint candidates are expanded
    /// dynamically, then resolved through this same registry before they are returned.
    /// </summary>
    /// <param name="prefix">The optional ordinal candidate prefix.</param>
    /// <param name="theme">The immutable project theme.</param>
    /// <returns>Matching items in deterministic compiler order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="theme"/> is null.</exception>
    public UtilityCollection<UtilityClassMetadata> GetCompletions(
        string? prefix,
        UtilityTheme theme)
    {
        if (theme is null)
        {
            throw new ArgumentNullException(nameof(theme));
        }

        var completionItems = ReferenceEquals(theme, UtilityTheme.Default)
            ? CompletionItems
            : CreateCompletionItems(theme);
        if (string.IsNullOrEmpty(prefix))
        {
            return completionItems;
        }

        return new UtilityCollection<UtilityClassMetadata>(
            completionItems.Where(
                item => item.CandidateText.StartsWith(
                    prefix,
                    StringComparison.Ordinal)));
    }

    internal bool TryGetRegistration(
        string root,
        out UtilityRegisteredDefinition? registration) =>
        definitionsByRoot.TryGetValue(root, out registration);

    private UtilityCollection<UtilityClassMetadata> CreateCompletionItems(
        UtilityTheme theme)
    {
        var candidates = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in Definitions)
        {
            foreach (var candidate in definition.CompletionCandidates)
            {
                candidates.Add(candidate);
            }

            AddThemeCandidates(
                candidates,
                definition,
                theme);
        }

        var unprefixedCandidates = candidates.ToArray();
        foreach (var breakpoint in theme.Breakpoints)
        {
            foreach (var candidate in unprefixedCandidates)
            {
                candidates.Add(breakpoint.Name + ":" + candidate);
            }
        }

        var completionItems = new List<UtilityClassMetadata>();
        foreach (var candidate in candidates)
        {
            var candidateText = AddConfiguredPrefix(
                candidate,
                theme.Prefix);
            var result = Resolve(
                candidateText,
                theme,
                CancellationToken.None);
            if (result.IsSuccess && result.Metadata is not null)
            {
                completionItems.Add(result.Metadata);
            }
        }

        return new UtilityCollection<UtilityClassMetadata>(
            completionItems
                .Distinct()
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.CandidateText, StringComparer.Ordinal));
    }

    private static void AddThemeCandidates(
        ISet<string> candidates,
        UtilityDefinition definition,
        UtilityTheme theme)
    {
        var root = definition.Root;
        if (UsesSpacingTheme(root))
        {
            foreach (var token in theme.Spacing)
            {
                candidates.Add(root + "-" + token.Name);
                if (definition.SupportsNegativeValues &&
                    token.Name != "0")
                {
                    candidates.Add("-" + root + "-" + token.Name);
                }
            }
        }

        if (UsesColorTheme(root))
        {
            foreach (var token in theme.Colors)
            {
                candidates.Add(root + "-" + token.Name);
            }
        }

        if (root == "text")
        {
            foreach (var token in theme.FontSizes)
            {
                candidates.Add("text-" + token.Name);
            }
        }

        if (root == "font")
        {
            foreach (var token in theme.FontWeights)
            {
                candidates.Add("font-" + token.Name);
            }
        }

        if (root.StartsWith("rounded", StringComparison.Ordinal))
        {
            foreach (var token in theme.Radii)
            {
                candidates.Add(
                    token.Name == "DEFAULT"
                        ? root
                        : root + "-" + token.Name);
            }
        }

        var additionalNamespace = GetAdditionalThemeNamespace(root);
        if (additionalNamespace is not null)
        {
            AddNamespaceCandidates(
                candidates,
                definition,
                theme,
                additionalNamespace);
        }
    }

    private static void AddNamespaceCandidates(
        ISet<string> candidates,
        UtilityDefinition definition,
        UtilityTheme theme,
        string namespaceName)
    {
        foreach (var token in theme.GetNamespaceTokens(namespaceName))
        {
            var candidate = token.Name == "DEFAULT"
                ? definition.Root
                : definition.Root + "-" + token.Name;
            candidates.Add(candidate);
            if (definition.SupportsNegativeValues &&
                token.Name is not "0" and not "DEFAULT")
            {
                candidates.Add("-" + candidate);
            }
        }
    }

    private static string? GetAdditionalThemeNamespace(string root)
    {
        if (UsesContainerTheme(root))
        {
            return UtilityThemeNamespaceNames.Container;
        }

        return root switch
        {
            "font" => UtilityThemeNamespaceNames.FontFamily,
            "tracking" => UtilityThemeNamespaceNames.LetterSpacing,
            "leading" => UtilityThemeNamespaceNames.LineHeight,
            "tab" => UtilityThemeNamespaceNames.TabSize,
            "shadow" => UtilityThemeNamespaceNames.Shadow,
            "inset-shadow" => UtilityThemeNamespaceNames.InsetShadow,
            "text-shadow" => UtilityThemeNamespaceNames.TextShadow,
            "drop-shadow" => UtilityThemeNamespaceNames.DropShadow,
            "blur" or "backdrop-blur" => UtilityThemeNamespaceNames.Blur,
            "perspective" => UtilityThemeNamespaceNames.Perspective,
            "zoom" => UtilityThemeNamespaceNames.Zoom,
            "aspect" => UtilityThemeNamespaceNames.AspectRatio,
            "ease" => UtilityThemeNamespaceNames.TransitionTiming,
            "animate" => UtilityThemeNamespaceNames.Animation,
            _ => null,
        };
    }

    private static bool UsesContainerTheme(string root) =>
        root is
            "columns" or
            "basis" or
            "w" or
            "h" or
            "min-w" or
            "min-h" or
            "max-w" or
            "max-h" or
            "size" or
            "inline" or
            "block" or
            "min-inline" or
            "min-block" or
            "max-inline" or
            "max-block";

    private static bool UsesSpacingTheme(string root) =>
        root is
            "inset" or
            "inset-x" or
            "inset-y" or
            "inset-s" or
            "inset-e" or
            "inset-bs" or
            "inset-be" or
            "top" or
            "right" or
            "bottom" or
            "left" or
            "m" or
            "mx" or
            "my" or
            "mt" or
            "mr" or
            "mb" or
            "ml" or
            "ms" or
            "me" or
            "mbs" or
            "mbe" or
            "p" or
            "px" or
            "py" or
            "pt" or
            "pr" or
            "pb" or
            "pl" or
            "ps" or
            "pe" or
            "pbs" or
            "pbe" or
            "space-x" or
            "space-y" or
            "gap" or
            "gap-x" or
            "gap-y" or
            "border-spacing" or
            "border-spacing-x" or
            "border-spacing-y" or
            "scroll-m" or
            "scroll-mx" or
            "scroll-my" or
            "scroll-ms" or
            "scroll-me" or
            "scroll-mbs" or
            "scroll-mbe" or
            "scroll-mt" or
            "scroll-mr" or
            "scroll-mb" or
            "scroll-ml" or
            "scroll-p" or
            "scroll-px" or
            "scroll-py" or
            "scroll-ps" or
            "scroll-pe" or
            "scroll-pbs" or
            "scroll-pbe" or
            "scroll-pt" or
            "scroll-pr" or
            "scroll-pb" or
            "scroll-pl" or
            "w" or
            "h" or
            "min-w" or
            "min-h" or
            "max-w" or
            "max-h" or
            "size" or
            "inline" or
            "block" or
            "min-inline" or
            "min-block" or
            "max-inline" or
            "max-block" or
            "basis" or
            "indent" or
            "underline-offset" or
            "translate-z" or
            "mask-x-from" or
            "mask-x-to" or
            "mask-y-from" or
            "mask-y-to" or
            "mask-t-from" or
            "mask-t-to" or
            "mask-r-from" or
            "mask-r-to" or
            "mask-b-from" or
            "mask-b-to" or
            "mask-l-from" or
            "mask-l-to" or
            "mask-linear-from" or
            "mask-linear-to" or
            "mask-radial-from" or
            "mask-radial-to" or
            "mask-conic-from" or
            "mask-conic-to";

    private static bool UsesColorTheme(string root) =>
        root is
            "text" or
            "bg" or
            "border" or
            "border-x" or
            "border-y" or
            "border-t" or
            "border-r" or
            "border-b" or
            "border-l" or
            "border-s" or
            "border-e" or
            "border-bs" or
            "border-be" or
            "outline" or
            "accent" or
            "caret" or
            "placeholder" or
            "decoration" or
            "divide" or
            "ring" or
            "inset-ring" or
            "ring-offset" or
            "shadow" or
            "inset-shadow" or
            "text-shadow" or
            "scrollbar-thumb" or
            "scrollbar-track" or
            "from" or
            "via" or
            "to" or
            "mask-x-from" or
            "mask-x-to" or
            "mask-y-from" or
            "mask-y-to" or
            "mask-t-from" or
            "mask-t-to" or
            "mask-r-from" or
            "mask-r-to" or
            "mask-b-from" or
            "mask-b-to" or
            "mask-l-from" or
            "mask-l-to" or
            "mask-linear-from" or
            "mask-linear-to" or
            "mask-radial-from" or
            "mask-radial-to" or
            "mask-conic-from" or
            "mask-conic-to" or
            "fill" or
            "stroke";

    private static string AddConfiguredPrefix(
        string candidate,
        string? prefix) =>
        string.IsNullOrEmpty(prefix) ||
        candidate.StartsWith(prefix + ":", StringComparison.Ordinal)
            ? candidate
            : prefix + ":" + candidate;

    private static UtilityRegisteredDefinition[] CreateBuiltInRegistrations()
    {
        var registrations = new List<UtilityRegisteredDefinition>();

        AddMany(
            registrations,
            UtilityResolverKind.Display,
            "Sets the display box type.",
            100,
            false,
            new[]
            {
                "block",
                "inline-block",
                "inline",
                "inline-flex",
                "grid",
                "inline-grid",
                "contents",
                "flow-root",
                "hidden",
                "table",
            },
            new[]
            {
                "block",
                "block-4",
                "inline",
                "inline-1/2",
                "inline-block",
                "inline-flex",
                "inline-grid",
                "inline-table",
                "grid",
                "hidden",
                "table",
                "table-cell",
                "table-row",
                "table-fixed",
            });
        AddMany(
            registrations,
            UtilityResolverKind.Position,
            "Sets the positioning scheme.",
            200,
            false,
            new[] { "static", "fixed", "absolute", "relative", "sticky" });
        AddMany(
            registrations,
            UtilityResolverKind.PositionOffset,
            "Sets a positioned inset.",
            210,
            true,
            new[]
            {
                "inset",
                "inset-x",
                "inset-y",
                "top",
                "right",
                "bottom",
                "left",
                "start",
                "inset-s",
                "inset-e",
                "inset-bs",
                "inset-be",
            },
            new[]
            {
                "top-0",
                "right-4",
                "-bottom-2",
                "inset-x-4",
                "inset-s-2",
                "inset-bs-full",
            });
        Add(
            registrations,
            "container",
            UtilityResolverKind.Container,
            "Creates a responsive max-width container.",
            220,
            false,
            "container");
        Add(
            registrations,
            "@container",
            UtilityResolverKind.Container,
            "Establishes a named or unnamed query container.",
            221,
            false,
            "@container",
            "@container-size",
            "@container/sidebar",
            "@container-size/sidebar");

        Add(
            registrations,
            "flex",
            UtilityResolverKind.Flex,
            "Creates a flex box or sets the flex shorthand.",
            300,
            false,
            "flex",
            "flex-1");
        AddMany(
            registrations,
            UtilityResolverKind.Flex,
            "Sets a predefined flex shorthand.",
            301,
            false,
            new[] { "flex-auto", "flex-initial", "flex-none" });
        AddMany(
            registrations,
            UtilityResolverKind.FlexDirection,
            "Sets flex direction or wrapping.",
            310,
            false,
            new[]
            {
                "flex-row",
                "flex-row-reverse",
                "flex-col",
                "flex-col-reverse",
            });
        Add(
            registrations,
            "grow",
            UtilityResolverKind.Grow,
            "Sets the flex grow factor.",
            320,
            false,
            "grow",
            "grow-0");
        Add(
            registrations,
            "shrink",
            UtilityResolverKind.Shrink,
            "Sets the flex shrink factor.",
            321,
            false,
            "shrink",
            "shrink-0");
        AddMany(
            registrations,
            UtilityResolverKind.Alignment,
            "Aligns flex or grid items.",
            330,
            false,
            new[]
            {
                "items",
                "justify",
                "self",
                "content",
                "justify-items",
                "justify-self",
                "place-content",
                "place-items",
                "place-self",
            },
            new[]
            {
                "items-center",
                "justify-between",
                "self-start",
                "content-around",
                "content-['Open']",
                "justify-items-stretch",
                "place-items-center",
                "place-self-end",
            });

        Add(
            registrations,
            "grid-cols",
            UtilityResolverKind.GridColumns,
            "Sets explicit grid columns.",
            400,
            false,
            "grid-cols-1",
            "grid-cols-2",
            "grid-cols-3",
            "grid-cols-[200px_1fr]");
        Add(
            registrations,
            "grid-rows",
            UtilityResolverKind.GridRows,
            "Sets explicit grid rows.",
            401,
            false,
            "grid-rows-1",
            "grid-rows-2");
        AddMany(
            registrations,
            UtilityResolverKind.GridSpan,
            "Sets a grid column or row span.",
            410,
            false,
            new[] { "col-span", "row-span" },
            new[] { "col-span-2", "row-span-full" });

        AddMany(
            registrations,
            UtilityResolverKind.Spacing,
            "Sets margin from the spacing theme.",
            500,
            true,
            new[]
            {
                "m",
                "mx",
                "my",
                "mt",
                "mr",
                "mb",
                "ml",
                "ms",
                "me",
                "mbs",
                "mbe",
            },
            new[] { "m-0", "mx-4", "mt-2", "-mb-4", "mbs-2", "mbe-auto" });
        AddMany(
            registrations,
            UtilityResolverKind.Spacing,
            "Sets padding from the spacing theme.",
            510,
            false,
            new[]
            {
                "p",
                "px",
                "py",
                "pt",
                "pr",
                "pb",
                "pl",
                "ps",
                "pe",
                "pbs",
                "pbe",
            },
            new[] { "p-4", "px-6", "py-2", "pbs-3", "pbe-3" });
        AddMany(
            registrations,
            UtilityResolverKind.Spacing,
            "Sets grid or flex gap from the spacing theme.",
            520,
            false,
            new[] { "gap", "gap-x", "gap-y" },
            new[] { "gap-4", "gap-x-2", "gap-y-[1.5rem]" });
        AddMany(
            registrations,
            UtilityResolverKind.SiblingSpacing,
            "Sets spacing between sibling elements.",
            525,
            true,
            new[] { "space-x", "space-y" },
            new[] { "space-x-4", "-space-y-2" });
        Add(
            registrations,
            "divide",
            UtilityResolverKind.Divide,
            "Sets the border color between sibling elements.",
            535,
            false,
            "divide-gray-200",
            "divide-blue-500/50");
        AddMany(
            registrations,
            UtilityResolverKind.Divide,
            "Sets the border width between sibling elements.",
            536,
            false,
            new[] { "divide-x", "divide-y" },
            new[] { "divide-x", "divide-x-2", "divide-y-4" });

        AddMany(
            registrations,
            UtilityResolverKind.Sizing,
            "Sets width, height, or both dimensions.",
            600,
            false,
            new[]
            {
                "w",
                "h",
                "min-w",
                "min-h",
                "max-w",
                "max-h",
                "size",
                "min-inline",
                "min-block",
                "max-inline",
                "max-block",
            },
            new[]
            {
                "w-4",
                "w-1/2",
                "w-full",
                "h-screen",
                "size-8",
                "w-[32px]",
                "min-w-0",
                "max-w-full",
                "inline-1/2",
                "block-dvh",
            });

        Add(
            registrations,
            "text",
            UtilityResolverKind.Text,
            "Sets text color or font size.",
            700,
            false,
            "text-sm",
            "text-lg",
            "text-gray-900",
            "text-blue-500",
            "text-[color:var(--brand)]");
        Add(
            registrations,
            "font",
            UtilityResolverKind.Font,
            "Sets font weight.",
            710,
            false,
            "font-normal",
            "font-medium",
            "font-semibold",
            "font-bold");
        Add(
            registrations,
            "bg",
            UtilityResolverKind.Background,
            "Sets the background color.",
            720,
            false,
            "bg-white",
            "bg-gray-100",
            "bg-blue-500",
            "bg-red-500/50",
            "bg-fixed",
            "bg-clip-text",
            "bg-origin-padding",
            "bg-cover",
            "bg-center",
            "bg-no-repeat",
            "bg-none",
            "hover:bg-blue-500",
            "focus:bg-blue-500",
            "active:bg-blue-500",
            "disabled:bg-gray-100",
            "dark:bg-gray-900",
            "sm:bg-blue-500",
            "md:hover:bg-blue-500",
            "group-hover:bg-blue-500",
            "peer-disabled:bg-gray-100");
        AddMany(
            registrations,
            UtilityResolverKind.Border,
            "Sets border width or color.",
            730,
            false,
            new[]
            {
                "border",
                "border-x",
                "border-y",
                "border-t",
                "border-r",
                "border-b",
                "border-l",
                "border-s",
                "border-e",
                "border-bs",
                "border-be",
            },
            new[]
            {
                "border",
                "border-2",
                "border-gray-500",
                "border-blue-500/50",
                "border-s-2",
                "border-bs-red-500",
            });
        AddMany(
            registrations,
            UtilityResolverKind.Radius,
            "Sets border radius.",
            740,
            false,
            new[]
            {
                "rounded",
                "rounded-t",
                "rounded-r",
                "rounded-b",
                "rounded-l",
                "rounded-tl",
                "rounded-tr",
                "rounded-br",
                "rounded-bl",
                "rounded-s",
                "rounded-e",
                "rounded-bs",
                "rounded-be",
                "rounded-ss",
                "rounded-se",
                "rounded-es",
                "rounded-ee",
            },
            new[]
            {
                "rounded",
                "rounded-md",
                "rounded-lg",
                "rounded-full",
                "rounded-ss-lg",
                "rounded-ee-lg",
            });
        Add(
            registrations,
            "opacity",
            UtilityResolverKind.Opacity,
            "Sets element opacity.",
            750,
            false,
            "opacity-0",
            "opacity-50",
            "opacity-100",
            "opacity-[.72]",
            "disabled:opacity-50!");
        AddMany(
            registrations,
            UtilityResolverKind.Ring,
            "Sets an outline ring or its offset.",
            895,
            false,
            new[] { "ring", "inset-ring", "ring-offset" },
            new[]
            {
                "ring",
                "ring-2",
                "ring-blue-500",
                "ring-blue-500/50",
                "inset-ring",
                "inset-ring-2",
                "inset-ring-blue-500",
                "ring-offset-2",
                "ring-offset-white",
            });

        foreach (var definition in UtilityBuiltInCatalog.StaticDefinitions)
        {
            Add(
                registrations,
                definition.Candidate,
                UtilityResolverKind.Static,
                definition.Description,
                definition.Order,
                false,
                definition.Candidate);
        }

        foreach (var definition in UtilityBuiltInCatalog.KeywordDefinitions)
        {
            Add(
                registrations,
                definition.Root,
                UtilityResolverKind.Keyword,
                definition.Description,
                definition.Order,
                false,
                definition.CompletionCandidates.ToArray());
        }

        Add(
            registrations,
            "z",
            UtilityResolverKind.Integer,
            "Sets stacking order.",
            205,
            true,
            "z-auto",
            "z-0",
            "z-10",
            "z-50",
            "-z-10");
        Add(
            registrations,
            "order",
            UtilityResolverKind.Integer,
            "Sets flex or grid item order.",
            340,
            true,
            "order-first",
            "order-last",
            "order-none",
            "order-1",
            "-order-1");
        Add(
            registrations,
            "tab",
            UtilityResolverKind.Integer,
            "Sets the tab character width.",
            726,
            false,
            "tab-2",
            "tab-4",
            "tab-8",
            "tab-[3]");
        Add(
            registrations,
            "zoom",
            UtilityResolverKind.Integer,
            "Sets visual zoom.",
            1430,
            false,
            "zoom-50",
            "zoom-100",
            "zoom-125",
            "zoom-[1.1]");

        Add(
            registrations,
            "aspect",
            UtilityResolverKind.AspectRatio,
            "Sets an element aspect ratio.",
            150,
            false,
            "aspect-auto",
            "aspect-square",
            "aspect-video",
            "aspect-16/9",
            "aspect-[3/2]");
        Add(
            registrations,
            "columns",
            UtilityResolverKind.Columns,
            "Sets multi-column count or width.",
            151,
            false,
            "columns-auto",
            "columns-2",
            "columns-sm",
            "columns-[18rem]");
        Add(
            registrations,
            "basis",
            UtilityResolverKind.FlexBasis,
            "Sets the initial flex item size.",
            302,
            false,
            "basis-auto",
            "basis-full",
            "basis-4",
            "basis-1/2",
            "basis-[18rem]");

        AddMany(
            registrations,
            UtilityResolverKind.GridLine,
            "Sets explicit grid placement.",
            411,
            true,
            new[] { "col", "col-start", "col-end", "row", "row-start", "row-end" },
            new[]
            {
                "col-auto",
                "col-2",
                "-col-2",
                "col-start-1",
                "col-end-4",
                "row-auto",
                "row-2",
                "row-start-1",
                "row-end-4",
            });

        AddMany(
            registrations,
            UtilityResolverKind.Spacing,
            "Sets table border spacing.",
            525,
            false,
            new[] { "border-spacing", "border-spacing-x", "border-spacing-y" },
            new[] { "border-spacing-2", "border-spacing-x-4", "border-spacing-y-[3px]" });
        AddMany(
            registrations,
            UtilityResolverKind.Spacing,
            "Sets scroll margin.",
            530,
            true,
            new[]
            {
                "scroll-m",
                "scroll-mx",
                "scroll-my",
                "scroll-ms",
                "scroll-me",
                "scroll-mbs",
                "scroll-mbe",
                "scroll-mt",
                "scroll-mr",
                "scroll-mb",
                "scroll-ml",
            },
            new[] { "scroll-m-4", "-scroll-ms-2", "scroll-mbs-3" });
        AddMany(
            registrations,
            UtilityResolverKind.Spacing,
            "Sets scroll padding.",
            545,
            false,
            new[]
            {
                "scroll-p",
                "scroll-px",
                "scroll-py",
                "scroll-ps",
                "scroll-pe",
                "scroll-pbs",
                "scroll-pbe",
                "scroll-pt",
                "scroll-pr",
                "scroll-pb",
                "scroll-pl",
            },
            new[] { "scroll-p-4", "scroll-pe-2", "scroll-pbe-3" });

        AddMany(
            registrations,
            UtilityResolverKind.Typography,
            "Sets a typography property.",
            760,
            false,
            new[]
            {
                "leading",
                "line-clamp",
                "align",
                "list",
                "list-image",
                "decoration",
                "font-stretch",
                "font-features",
            },
            new[]
            {
                "leading-6",
                "leading-none",
                "line-clamp-3",
                "line-clamp-none",
                "align-middle",
                "list-disc",
                "list-inside",
                "list-image-[url(check.svg)]",
                "decoration-dashed",
                "decoration-2",
                "decoration-blue-500",
                "font-stretch-expanded",
                "font-features-[\"kern\"]",
            });
        AddMany(
            registrations,
            UtilityResolverKind.Typography,
            "Sets a negative-capable typography length.",
            780,
            true,
            new[] { "tracking", "indent", "underline-offset" },
            new[]
            {
                "tracking-wide",
                "-tracking-tight",
                "indent-4",
                "-indent-2",
                "underline-offset-4",
                "-underline-offset-2",
            });

        AddMany(
            registrations,
            UtilityResolverKind.Color,
            "Sets an interactive color property.",
            800,
            false,
            new[] { "accent", "caret", "placeholder" },
            new[]
            {
                "accent-blue-500",
                "accent-auto",
                "caret-red-500",
                "placeholder-gray-500",
            });

        Add(
            registrations,
            "bg-linear",
            UtilityResolverKind.Background,
            "Sets a linear-gradient direction and interpolation method.",
            825,
            true,
            "bg-linear-to-r",
            "bg-linear-45/oklab",
            "-bg-linear-45",
            "bg-linear-[45deg,red,blue]");
        Add(
            registrations,
            "bg-conic",
            UtilityResolverKind.Background,
            "Sets a conic-gradient starting angle and interpolation method.",
            826,
            true,
            "bg-conic",
            "bg-conic-90/longer",
            "-bg-conic-45");
        AddMany(
            registrations,
            UtilityResolverKind.Background,
            "Sets a background gradient, position, or size.",
            827,
            false,
            new[] { "bg-radial", "bg-position", "bg-size" },
            new[]
            {
                "bg-radial",
                "bg-radial/oklch",
                "bg-position-[center_top_1rem]",
                "bg-size-[12rem_8rem]",
            });
        AddMany(
            registrations,
            UtilityResolverKind.Background,
            "Sets a background gradient color stop or stop position.",
            835,
            false,
            new[] { "from", "via", "to" },
            new[]
            {
                "from-red-500",
                "from-25%",
                "via-blue-500/50",
                "via-[50px]",
                "to-transparent",
                "to-100%",
            });
        Add(
            registrations,
            "outline",
            UtilityResolverKind.Outline,
            "Sets outline width, style, or color.",
            880,
            false,
            "outline",
            "outline-none",
            "outline-hidden",
            "outline-2",
            "outline-dashed",
            "outline-blue-500",
            "outline-[3px]");
        Add(
            registrations,
            "outline-offset",
            UtilityResolverKind.Outline,
            "Sets outline offset.",
            881,
            true,
            "outline-offset-0",
            "outline-offset-2",
            "-outline-offset-2",
            "outline-offset-[3px]");

        AddMany(
            registrations,
            UtilityResolverKind.Shadow,
            "Sets a box, inset, or text shadow.",
            920,
            false,
            new[] { "shadow", "inset-shadow", "text-shadow" },
            new[]
            {
                "shadow",
                "shadow-sm",
                "shadow-lg",
                "shadow-none",
                "shadow-blue-500/50",
                "inset-shadow-sm",
                "text-shadow-sm",
                "text-shadow-none",
            });
        Add(
            registrations,
            "mask",
            UtilityResolverKind.Mask,
            "Sets mask image geometry.",
            950,
            false,
            "mask-none",
            "mask-add",
            "mask-alpha",
            "mask-type-luminance",
            "mask-cover",
            "mask-center",
            "mask-no-repeat",
            "mask-circle",
            "mask-radial-closest-side",
            "mask-[url(mask.svg)]");
        Add(
            registrations,
            "mask-linear",
            UtilityResolverKind.Mask,
            "Sets a composable linear mask angle.",
            951,
            true,
            "mask-linear-0",
            "mask-linear-45",
            "-mask-linear-45",
            "mask-linear-[45deg]");
        AddMany(
            registrations,
            UtilityResolverKind.Mask,
            "Sets composable radial mask geometry.",
            952,
            false,
            new[] { "mask-radial", "mask-radial-at" },
            new[]
            {
                "mask-radial-[40px_80px]",
                "mask-radial-at-center",
                "mask-radial-at-[25%_25%]",
            });
        Add(
            registrations,
            "mask-conic",
            UtilityResolverKind.Mask,
            "Sets a composable conic mask angle.",
            953,
            true,
            "mask-conic-0",
            "mask-conic-90",
            "-mask-conic-45",
            "mask-conic-[45deg]");
        AddMany(
            registrations,
            UtilityResolverKind.Mask,
            "Sets an edge-mask color stop or stop position.",
            970,
            false,
            new[]
            {
                "mask-x-from",
                "mask-x-to",
                "mask-y-from",
                "mask-y-to",
                "mask-t-from",
                "mask-t-to",
                "mask-r-from",
                "mask-r-to",
                "mask-b-from",
                "mask-b-to",
                "mask-l-from",
                "mask-l-to",
            },
            new[]
            {
                "mask-x-from-20",
                "mask-x-to-transparent",
                "mask-y-from-black",
                "mask-y-to-80%",
                "mask-t-from-25%",
                "mask-t-to-transparent",
                "mask-r-from-25%",
                "mask-r-to-transparent",
                "mask-b-from-25%",
                "mask-b-to-transparent",
                "mask-l-from-25%",
                "mask-l-to-transparent",
            });
        AddMany(
            registrations,
            UtilityResolverKind.Mask,
            "Sets a gradient-mask color stop or stop position.",
            982,
            false,
            new[]
            {
                "mask-linear-from",
                "mask-linear-to",
                "mask-radial-from",
                "mask-radial-to",
                "mask-conic-from",
                "mask-conic-to",
            },
            new[]
            {
                "mask-linear-from-black",
                "mask-linear-to-100%",
                "mask-radial-from-20",
                "mask-radial-to-transparent",
                "mask-conic-from-blue-500/50",
                "mask-conic-to-100%",
            });

        AddMany(
            registrations,
            UtilityResolverKind.Filter,
            "Sets an element filter component.",
            1000,
            false,
            new[]
            {
                "filter",
                "blur",
                "brightness",
                "contrast",
                "drop-shadow",
                "grayscale",
                "invert",
                "saturate",
                "sepia",
            },
            new[]
            {
                "filter-none",
                "blur-sm",
                "brightness-75",
                "contrast-125",
                "drop-shadow-lg",
                "grayscale",
                "invert",
                "saturate-150",
                "sepia",
            });
        Add(
            registrations,
            "hue-rotate",
            UtilityResolverKind.Filter,
            "Sets the hue-rotate filter component.",
            1010,
            true,
            "hue-rotate-90",
            "-hue-rotate-30");
        AddMany(
            registrations,
            UtilityResolverKind.BackdropFilter,
            "Sets a backdrop-filter component.",
            1020,
            false,
            new[]
            {
                "backdrop-filter",
                "backdrop-blur",
                "backdrop-brightness",
                "backdrop-contrast",
                "backdrop-grayscale",
                "backdrop-invert",
                "backdrop-opacity",
                "backdrop-saturate",
                "backdrop-sepia",
            },
            new[]
            {
                "backdrop-filter-none",
                "backdrop-blur-sm",
                "backdrop-brightness-75",
                "backdrop-contrast-125",
                "backdrop-grayscale",
                "backdrop-invert",
                "backdrop-opacity-50",
                "backdrop-saturate-150",
                "backdrop-sepia",
            });
        Add(
            registrations,
            "backdrop-hue-rotate",
            UtilityResolverKind.BackdropFilter,
            "Sets the backdrop hue-rotate filter component.",
            1030,
            true,
            "backdrop-hue-rotate-90",
            "-backdrop-hue-rotate-30");

        AddMany(
            registrations,
            UtilityResolverKind.Transition,
            "Sets transition or animation behavior.",
            1100,
            false,
            new[] { "transition", "duration", "delay", "ease", "animate" },
            new[]
            {
                "transition",
                "transition-all",
                "transition-colors",
                "transition-none",
                "transition-discrete",
                "duration-150",
                "delay-75",
                "ease-in-out",
                "animate-none",
                "animate-[pulse_2s_infinite]",
            });

        AddMany(
            registrations,
            UtilityResolverKind.Transform,
            "Sets a transform component or perspective.",
            1200,
            true,
            new[]
            {
                "translate",
                "translate-x",
                "translate-y",
                "translate-z",
                "rotate",
                "rotate-x",
                "rotate-y",
                "rotate-z",
                "skew",
                "skew-x",
                "skew-y",
                "scale",
                "scale-x",
                "scale-y",
                "scale-z",
            },
            new[]
            {
                "translate-1/2",
                "-translate-x-4",
                "translate-z-px",
                "rotate-45",
                "-rotate-y-90",
                "rotate-z-45",
                "skew-x-6",
                "scale-105",
                "scale-x-75",
                "scale-z-50",
            });
        AddMany(
            registrations,
            UtilityResolverKind.Transform,
            "Sets transform origin, perspective, or rendering mode.",
            1220,
            false,
            new[]
            {
                "origin",
                "perspective",
                "perspective-origin",
                "transform",
            },
            new[]
            {
                "origin-center",
                "perspective-near",
                "perspective-origin-top",
                "transform-gpu",
                "transform-none",
                "transform-3d",
                "transform-border",
            });

        AddMany(
            registrations,
            UtilityResolverKind.ScrollbarColor,
            "Sets scrollbar thumb or track color.",
            1520,
            false,
            new[] { "scrollbar-thumb", "scrollbar-track" },
            new[]
            {
                "scrollbar-thumb-gray-500",
                "scrollbar-thumb-blue-500/50",
                "scrollbar-track-gray-200",
            });

        Add(
            registrations,
            "fill",
            UtilityResolverKind.Svg,
            "Sets SVG fill color.",
            1600,
            false,
            "fill-none",
            "fill-current",
            "fill-blue-500",
            "fill-(--brand-color)");
        Add(
            registrations,
            "stroke",
            UtilityResolverKind.Svg,
            "Sets SVG stroke color or width.",
            1601,
            false,
            "stroke-none",
            "stroke-current",
            "stroke-blue-500",
            "stroke-2",
            "stroke-[3px]");

        return registrations.ToArray();
    }

    private static void AddMany(
        ICollection<UtilityRegisteredDefinition> registrations,
        UtilityResolverKind resolverKind,
        string description,
        int firstOrder,
        bool supportsNegativeValues,
        IEnumerable<string> roots,
        IEnumerable<string>? completionCandidates = null)
    {
        var rootArray = roots.ToArray();
        var candidateArray = completionCandidates?.ToArray();
        for (var index = 0; index < rootArray.Length; index++)
        {
            var root = rootArray[index];
            var completions = (candidateArray is null
                ? new[] { root }
                : candidateArray.Where(
                    candidate =>
                        IsCompletionForRoot(candidate, root)))
                .ToArray();
            if (completions.Length == 0)
            {
                completions =
                    new[]
                    {
                        CreateFallbackCompletionCandidate(
                            root,
                            resolverKind),
                    };
            }

            Add(
                registrations,
                root,
                resolverKind,
                description,
                firstOrder + index,
                supportsNegativeValues,
                completions);
        }
    }

    private static string CreateFallbackCompletionCandidate(
        string root,
        UtilityResolverKind resolverKind) =>
        resolverKind switch
        {
            UtilityResolverKind.PositionOffset or
            UtilityResolverKind.Spacing or
            UtilityResolverKind.Sizing or
            UtilityResolverKind.Border =>
                root + "-0",
            UtilityResolverKind.Alignment =>
                root + "-center",
            UtilityResolverKind.Radius =>
                root + "-sm",
            UtilityResolverKind.Transform when
                root.StartsWith("scale", StringComparison.Ordinal) =>
                root + "-100",
            UtilityResolverKind.Transform =>
                root + "-0",
            _ => root,
        };

    private static bool IsCompletionForRoot(
        string candidate,
        string root)
    {
        var baseCandidate = candidate;
        var variantSeparator = baseCandidate.LastIndexOf(':');
        if (variantSeparator >= 0)
        {
            baseCandidate = baseCandidate.Substring(variantSeparator + 1);
        }

        if (baseCandidate.StartsWith("-", StringComparison.Ordinal))
        {
            baseCandidate = baseCandidate.Substring(1);
        }

        return baseCandidate == root ||
               baseCandidate.StartsWith(root + "-", StringComparison.Ordinal);
    }

    private static void Add(
        ICollection<UtilityRegisteredDefinition> registrations,
        string root,
        UtilityResolverKind resolverKind,
        string description,
        int order,
        bool supportsNegativeValues,
        params string[] completionCandidates)
    {
        registrations.Add(
            new UtilityRegisteredDefinition(
                new UtilityDefinition(
                    root,
                    description,
                    order,
                    supportsNegativeValues,
                    new UtilityCollection<string>(completionCandidates)),
                resolverKind));
    }
}
