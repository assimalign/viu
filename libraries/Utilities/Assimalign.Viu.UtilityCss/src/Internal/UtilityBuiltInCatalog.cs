using System;
using System.Collections.Generic;
using System.Linq;

namespace Assimalign.Viu.UtilityCss;

internal sealed class UtilityStaticDefinitionData
{
    public UtilityStaticDefinitionData(
        string candidate,
        string description,
        int order,
        params UtilityCssDeclaration[] declarations)
    {
        Candidate = candidate;
        Description = description;
        Order = order;
        Declarations = declarations;
    }

    public string Candidate { get; }

    public string Description { get; }

    public int Order { get; }

    public IReadOnlyList<UtilityCssDeclaration> Declarations { get; }
}

internal sealed class UtilityKeywordDefinitionData
{
    private readonly Dictionary<string, UtilityCssDeclaration[]> declarationsByValue;

    public UtilityKeywordDefinitionData(
        string root,
        string description,
        int order,
        bool supportsArbitraryValues,
        string? arbitraryProperty,
        IEnumerable<KeyValuePair<string, UtilityCssDeclaration[]>> declarationsByValue,
        params string[] completionCandidates)
    {
        Root = root;
        Description = description;
        Order = order;
        SupportsArbitraryValues = supportsArbitraryValues;
        ArbitraryProperty = arbitraryProperty;
        this.declarationsByValue = new Dictionary<string, UtilityCssDeclaration[]>(
            StringComparer.Ordinal);
        foreach (var declaration in declarationsByValue)
        {
            this.declarationsByValue.Add(
                declaration.Key,
                declaration.Value);
        }
        CompletionCandidates = completionCandidates;
    }

    public string Root { get; }

    public string Description { get; }

    public int Order { get; }

    public bool SupportsArbitraryValues { get; }

    public string? ArbitraryProperty { get; }

    public IReadOnlyList<string> CompletionCandidates { get; }

    public bool TryGetDeclarations(
        string value,
        out UtilityCssDeclaration[]? declarations) =>
        declarationsByValue.TryGetValue(value, out declarations);
}

internal static class UtilityBuiltInCatalog
{
    private static readonly UtilityStaticDefinitionData[] StaticDefinitionsValue =
        CreateStaticDefinitions();
    private static readonly UtilityKeywordDefinitionData[] KeywordDefinitionsValue =
        CreateKeywordDefinitions();
    private static readonly Dictionary<string, UtilityStaticDefinitionData> StaticByCandidate =
        StaticDefinitionsValue.ToDictionary(
            definition => definition.Candidate,
            StringComparer.Ordinal);
    private static readonly Dictionary<string, UtilityKeywordDefinitionData> KeywordByRoot =
        KeywordDefinitionsValue.ToDictionary(
            definition => definition.Root,
            StringComparer.Ordinal);

    public static IReadOnlyList<UtilityStaticDefinitionData> StaticDefinitions =>
        StaticDefinitionsValue;

    public static IReadOnlyList<UtilityKeywordDefinitionData> KeywordDefinitions =>
        KeywordDefinitionsValue;

    public static IEnumerable<string> KeywordRoots => KeywordByRoot.Keys;

    public static bool IsStaticCandidate(string candidate) =>
        StaticByCandidate.ContainsKey(candidate);

    public static bool TryGetStatic(
        string candidate,
        out UtilityStaticDefinitionData? definition) =>
        StaticByCandidate.TryGetValue(candidate, out definition);

    public static bool TryGetKeyword(
        string root,
        out UtilityKeywordDefinitionData? definition) =>
        KeywordByRoot.TryGetValue(root, out definition);

    private static UtilityStaticDefinitionData[] CreateStaticDefinitions() =>
        new[]
        {
            Static(
                "sr-only",
                "Visually hides content while preserving screen-reader access.",
                10,
                ("position", "absolute"),
                ("width", "1px"),
                ("height", "1px"),
                ("padding", "0"),
                ("margin", "-1px"),
                ("overflow", "hidden"),
                ("clip-path", "inset(50%)"),
                ("white-space", "nowrap"),
                ("border-width", "0")),
            Static(
                "not-sr-only",
                "Restores content hidden by the screen-reader-only helper.",
                11,
                ("position", "static"),
                ("width", "auto"),
                ("height", "auto"),
                ("padding", "0"),
                ("margin", "0"),
                ("overflow", "visible"),
                ("clip-path", "none"),
                ("white-space", "normal")),
            Static("visible", "Makes an element visible.", 90, ("visibility", "visible")),
            Static("invisible", "Hides an element while preserving layout.", 91, ("visibility", "hidden")),
            Static("collapse", "Collapses a table row or column.", 92, ("visibility", "collapse")),
            Static("isolate", "Creates a new stacking context.", 93, ("isolation", "isolate")),
            Static("box-border", "Includes border and padding in declared dimensions.", 94, ("box-sizing", "border-box")),
            Static("box-content", "Uses content-box dimension sizing.", 95, ("box-sizing", "content-box")),
            Static(
                "box-decoration-slice",
                "Slices box decorations across fragments.",
                96,
                ("-webkit-box-decoration-break", "slice"),
                ("box-decoration-break", "slice")),
            Static(
                "box-decoration-clone",
                "Clones box decorations across fragments.",
                97,
                ("-webkit-box-decoration-break", "clone"),
                ("box-decoration-break", "clone")),
            Static("inline-table", "Creates an inline table box.", 108, ("display", "inline-table")),
            Static("list-item", "Creates a list-item box.", 109, ("display", "list-item")),
            Static(
                "break-normal",
                "Restores normal word-breaking behavior.",
                144,
                ("overflow-wrap", "normal"),
                ("word-break", "normal")),
            Static("break-words", "Allows long words to wrap.", 145, ("overflow-wrap", "break-word")),
            Static("break-all", "Allows words to break between any two characters.", 146, ("word-break", "break-all")),
            Static("break-keep", "Prevents breaks inside CJK text.", 147, ("word-break", "keep-all")),
            Static("content-center-safe", "Safely centers align-content.", 347, ("align-content", "safe center")),
            Static("content-end-safe", "Safely aligns content at the flex end.", 348, ("align-content", "safe flex-end")),
            Static("items-center-safe", "Safely centers items.", 349, ("align-items", "safe center")),
            Static("items-end-safe", "Safely aligns items at the flex end.", 350, ("align-items", "safe flex-end")),
            Static("items-baseline-last", "Aligns items to the last baseline.", 351, ("align-items", "last baseline")),
            Static("justify-center-safe", "Safely centers justified content.", 352, ("justify-content", "safe center")),
            Static("justify-end-safe", "Safely justifies content at the flex end.", 353, ("justify-content", "safe flex-end")),
            Static("justify-items-center-safe", "Safely centers justified items.", 354, ("justify-items", "safe center")),
            Static("justify-items-end-safe", "Safely aligns justified items at the end.", 355, ("justify-items", "safe end")),
            Static("justify-self-center-safe", "Safely centers an item on its inline axis.", 356, ("justify-self", "safe center")),
            Static("justify-self-end-safe", "Safely aligns an item at the inline end.", 357, ("justify-self", "safe flex-end")),
            Static("place-content-center-safe", "Safely centers placed content.", 358, ("place-content", "safe center")),
            Static("place-content-end-safe", "Safely aligns placed content at the end.", 359, ("place-content", "safe end")),
            Static("place-items-center-safe", "Safely centers placed items.", 360, ("place-items", "safe center")),
            Static("place-items-end-safe", "Safely aligns placed items at the end.", 361, ("place-items", "safe end")),
            Static("place-self-center-safe", "Safely centers one placed item.", 362, ("place-self", "safe center")),
            Static("place-self-end-safe", "Safely aligns one placed item at the end.", 363, ("place-self", "safe end")),
            Static("self-center-safe", "Safely centers one aligned item.", 364, ("align-self", "safe center")),
            Static("self-end-safe", "Safely aligns one item at the flex end.", 365, ("align-self", "safe flex-end")),
            Static("self-baseline-last", "Aligns one item to the last baseline.", 366, ("align-self", "last baseline")),
            Static(
                "space-x-reverse",
                "Reverses horizontal sibling spacing.",
                525,
                ("--tw-space-x-reverse", "1")),
            Static(
                "space-y-reverse",
                "Reverses vertical sibling spacing.",
                526,
                ("--tw-space-y-reverse", "1")),
            Static("border-collapse", "Collapses adjacent table borders.", 760, ("border-collapse", "collapse")),
            Static("border-separate", "Keeps adjacent table borders separate.", 761, ("border-collapse", "separate")),
            Static(
                "antialiased",
                "Uses grayscale font smoothing.",
                705,
                ("-webkit-font-smoothing", "antialiased"),
                ("-moz-osx-font-smoothing", "grayscale")),
            Static(
                "subpixel-antialiased",
                "Uses subpixel font smoothing.",
                706,
                ("-webkit-font-smoothing", "auto"),
                ("-moz-osx-font-smoothing", "auto")),
            Static("italic", "Uses italic font style.", 707, ("font-style", "italic")),
            Static("not-italic", "Uses normal font style.", 708, ("font-style", "normal")),
            Static(
                "ordinal",
                "Enables ordinal font variants.",
                709,
                ("--tw-ordinal", "ordinal"),
                ("font-variant-numeric", "var(--tw-ordinal,) var(--tw-slashed-zero,) var(--tw-numeric-figure,) var(--tw-numeric-spacing,) var(--tw-numeric-fraction,)")),
            Static(
                "slashed-zero",
                "Enables slashed-zero glyphs.",
                710,
                ("--tw-slashed-zero", "slashed-zero"),
                ("font-variant-numeric", "var(--tw-ordinal,) var(--tw-slashed-zero,) var(--tw-numeric-figure,) var(--tw-numeric-spacing,) var(--tw-numeric-fraction,)")),
            Static(
                "lining-nums",
                "Uses lining numeral glyphs.",
                711,
                ("--tw-numeric-figure", "lining-nums"),
                ("font-variant-numeric", "var(--tw-ordinal,) var(--tw-slashed-zero,) var(--tw-numeric-figure,) var(--tw-numeric-spacing,) var(--tw-numeric-fraction,)")),
            Static(
                "oldstyle-nums",
                "Uses old-style numeral glyphs.",
                712,
                ("--tw-numeric-figure", "oldstyle-nums"),
                ("font-variant-numeric", "var(--tw-ordinal,) var(--tw-slashed-zero,) var(--tw-numeric-figure,) var(--tw-numeric-spacing,) var(--tw-numeric-fraction,)")),
            Static(
                "proportional-nums",
                "Uses proportional numeral spacing.",
                713,
                ("--tw-numeric-spacing", "proportional-nums"),
                ("font-variant-numeric", "var(--tw-ordinal,) var(--tw-slashed-zero,) var(--tw-numeric-figure,) var(--tw-numeric-spacing,) var(--tw-numeric-fraction,)")),
            Static(
                "tabular-nums",
                "Uses tabular numeral spacing.",
                714,
                ("--tw-numeric-spacing", "tabular-nums"),
                ("font-variant-numeric", "var(--tw-ordinal,) var(--tw-slashed-zero,) var(--tw-numeric-figure,) var(--tw-numeric-spacing,) var(--tw-numeric-fraction,)")),
            Static(
                "diagonal-fractions",
                "Uses diagonal fraction glyphs.",
                715,
                ("--tw-numeric-fraction", "diagonal-fractions"),
                ("font-variant-numeric", "var(--tw-ordinal,) var(--tw-slashed-zero,) var(--tw-numeric-figure,) var(--tw-numeric-spacing,) var(--tw-numeric-fraction,)")),
            Static(
                "stacked-fractions",
                "Uses stacked fraction glyphs.",
                716,
                ("--tw-numeric-fraction", "stacked-fractions"),
                ("font-variant-numeric", "var(--tw-ordinal,) var(--tw-slashed-zero,) var(--tw-numeric-figure,) var(--tw-numeric-spacing,) var(--tw-numeric-fraction,)")),
            Static("normal-nums", "Disables numeric font variants.", 716, ("font-variant-numeric", "normal")),
            Static("uppercase", "Transforms text to uppercase.", 717, ("text-transform", "uppercase")),
            Static("lowercase", "Transforms text to lowercase.", 718, ("text-transform", "lowercase")),
            Static("capitalize", "Capitalizes text.", 719, ("text-transform", "capitalize")),
            Static("normal-case", "Disables text transformation.", 720, ("text-transform", "none")),
            Static("underline", "Underlines text.", 721, ("text-decoration-line", "underline")),
            Static("overline", "Draws a line above text.", 722, ("text-decoration-line", "overline")),
            Static("line-through", "Draws a line through text.", 723, ("text-decoration-line", "line-through")),
            Static("no-underline", "Removes text decoration lines.", 724, ("text-decoration-line", "none")),
            Static(
                "truncate",
                "Truncates overflowing text with an ellipsis.",
                725,
                ("overflow", "hidden"),
                ("text-overflow", "ellipsis"),
                ("white-space", "nowrap")),
            Static("wrap-normal", "Restores normal overflow wrapping.", 732, ("overflow-wrap", "normal")),
            Static(
                "content-none",
                "Removes generated content.",
                733,
                ("--tw-content", "none"),
                ("content", "none")),
            Static("bg-top-left", "Positions the background at the top left.", 727, ("background-position", "left top")),
            Static("bg-top-right", "Positions the background at the top right.", 728, ("background-position", "right top")),
            Static("bg-bottom-left", "Positions the background at the bottom left.", 729, ("background-position", "left bottom")),
            Static("bg-bottom-right", "Positions the background at the bottom right.", 730, ("background-position", "right bottom")),
            Static("divide-x-reverse", "Reverses inline sibling borders.", 736, ("--tw-divide-x-reverse", "1")),
            Static("divide-y-reverse", "Reverses block sibling borders.", 737, ("--tw-divide-y-reverse", "1")),
            Static("text-shadow-initial", "Resets the text-shadow color override.", 892, ("--tw-text-shadow-color", "initial")),
            Static("shadow-initial", "Resets the box-shadow color override.", 893, ("--tw-shadow-color", "initial")),
            Static("inset-shadow-initial", "Resets the inset-shadow color override.", 894, ("--tw-inset-shadow-color", "initial")),
            Static("ring-inset", "Places the ring inside the element.", 895, ("--tw-ring-inset", "inset")),
            Static("mask-circle", "Uses a circular radial mask.", 950, ("--tw-mask-radial-shape", "circle")),
            Static("mask-ellipse", "Uses an elliptical radial mask.", 951, ("--tw-mask-radial-shape", "ellipse")),
            Static("mask-radial-closest-side", "Sizes a radial mask to the closest side.", 952, ("--tw-mask-radial-size", "closest-side")),
            Static("mask-radial-farthest-side", "Sizes a radial mask to the farthest side.", 953, ("--tw-mask-radial-size", "farthest-side")),
            Static("mask-radial-closest-corner", "Sizes a radial mask to the closest corner.", 954, ("--tw-mask-radial-size", "closest-corner")),
            Static("mask-radial-farthest-corner", "Sizes a radial mask to the farthest corner.", 955, ("--tw-mask-radial-size", "farthest-corner")),
            Static("mask-clip-border", "Clips a mask to the border box.", 956, ("mask-clip", "border-box")),
            Static("mask-clip-padding", "Clips a mask to the padding box.", 957, ("mask-clip", "padding-box")),
            Static("mask-clip-content", "Clips a mask to the content box.", 958, ("mask-clip", "content-box")),
            Static("mask-clip-fill", "Clips a mask to the SVG fill box.", 959, ("mask-clip", "fill-box")),
            Static("mask-clip-stroke", "Clips a mask to the SVG stroke box.", 960, ("mask-clip", "stroke-box")),
            Static("mask-clip-view", "Clips a mask to the SVG view box.", 961, ("mask-clip", "view-box")),
            Static("mask-no-clip", "Disables mask clipping.", 962, ("mask-clip", "no-clip")),
            Static("mask-origin-border", "Positions a mask from the border box.", 963, ("mask-origin", "border-box")),
            Static("mask-origin-padding", "Positions a mask from the padding box.", 964, ("mask-origin", "padding-box")),
            Static("mask-origin-content", "Positions a mask from the content box.", 965, ("mask-origin", "content-box")),
            Static("mask-origin-fill", "Positions a mask from the SVG fill box.", 966, ("mask-origin", "fill-box")),
            Static("mask-origin-stroke", "Positions a mask from the SVG stroke box.", 967, ("mask-origin", "stroke-box")),
            Static("mask-origin-view", "Positions a mask from the SVG view box.", 968, ("mask-origin", "view-box")),
            Static("duration-initial", "Resets the transition duration variable.", 1101, ("--tw-duration", "initial")),
            Static(
                "translate-3d",
                "Uses all three translate axes.",
                1201,
                ("translate", "var(--tw-translate-x) var(--tw-translate-y) var(--tw-translate-z)")),
            Static("rotate-none", "Disables independent rotation.", 1202, ("rotate", "none")),
            Static(
                "scale-3d",
                "Uses all three scale axes.",
                1203,
                ("scale", "var(--tw-scale-x) var(--tw-scale-y) var(--tw-scale-z)")),
            Static("scale-none", "Disables independent scaling.", 1204, ("scale", "none")),
            Static("via-none", "Removes the intermediate gradient stop.", 1205, ("--tw-gradient-via-stops", "initial")),
            Static("forced-color-adjust-auto", "Allows forced-color adjustments.", 1700, ("forced-color-adjust", "auto")),
            Static("forced-color-adjust-none", "Disables forced-color adjustments.", 1701, ("forced-color-adjust", "none")),
        };

    private static UtilityKeywordDefinitionData[] CreateKeywordDefinitions() =>
        new[]
        {
            Keyword("pointer-events", "Controls pointer-event targeting.", 20, "pointer-events", false,
                ("none", "none"), ("auto", "auto")),
            Keyword("isolation", "Controls stacking-context isolation.", 98, "isolation", false,
                ("auto", "auto")),
            Keyword("float", "Floats an element.", 110, "float", false,
                ("right", "right"), ("left", "left"), ("start", "inline-start"), ("end", "inline-end"), ("none", "none")),
            Keyword("clear", "Controls which floated sides are cleared.", 111, "clear", false,
                ("left", "left"), ("right", "right"), ("both", "both"), ("start", "inline-start"), ("end", "inline-end"), ("none", "none")),
            Keyword("overflow", "Controls element overflow.", 120, "overflow", false,
                ("auto", "auto"), ("hidden", "hidden"), ("clip", "clip"), ("visible", "visible"), ("scroll", "scroll")),
            Keyword("overflow-x", "Controls horizontal overflow.", 121, "overflow-x", false,
                ("auto", "auto"), ("hidden", "hidden"), ("clip", "clip"), ("visible", "visible"), ("scroll", "scroll")),
            Keyword("overflow-y", "Controls vertical overflow.", 122, "overflow-y", false,
                ("auto", "auto"), ("hidden", "hidden"), ("clip", "clip"), ("visible", "visible"), ("scroll", "scroll")),
            Keyword("overscroll", "Controls overscroll chaining.", 123, "overscroll-behavior", false,
                ("auto", "auto"), ("contain", "contain"), ("none", "none")),
            Keyword("overscroll-x", "Controls horizontal overscroll chaining.", 124, "overscroll-behavior-x", false,
                ("auto", "auto"), ("contain", "contain"), ("none", "none")),
            Keyword("overscroll-y", "Controls vertical overscroll chaining.", 125, "overscroll-behavior-y", false,
                ("auto", "auto"), ("contain", "contain"), ("none", "none")),
            Keyword("object", "Controls replaced-element fitting and position.", 130, "object-position", true,
                Entry("contain", ("object-fit", "contain")),
                Entry("cover", ("object-fit", "cover")),
                Entry("fill", ("object-fit", "fill")),
                Entry("none", ("object-fit", "none")),
                Entry("scale-down", ("object-fit", "scale-down")),
                Entry("bottom", ("object-position", "bottom")),
                Entry("center", ("object-position", "center")),
                Entry("left", ("object-position", "left")),
                Entry("left-bottom", ("object-position", "left bottom")),
                Entry("left-top", ("object-position", "left top")),
                Entry("right", ("object-position", "right")),
                Entry("right-bottom", ("object-position", "right bottom")),
                Entry("right-top", ("object-position", "right top")),
                Entry("top", ("object-position", "top"))),
            Keyword("break-after", "Controls column and page breaks after an element.", 140, "break-after", false,
                ("auto", "auto"), ("avoid", "avoid"), ("all", "all"), ("avoid-page", "avoid-page"),
                ("page", "page"), ("left", "left"), ("right", "right"), ("column", "column")),
            Keyword("break-before", "Controls column and page breaks before an element.", 141, "break-before", false,
                ("auto", "auto"), ("avoid", "avoid"), ("all", "all"), ("avoid-page", "avoid-page"),
                ("page", "page"), ("left", "left"), ("right", "right"), ("column", "column")),
            Keyword("break-inside", "Controls breaks inside an element.", 142, "break-inside", false,
                ("auto", "auto"), ("avoid", "avoid"), ("avoid-page", "avoid-page"), ("avoid-column", "avoid-column")),
            Keyword("contain", "Controls CSS containment.", 143, null, false,
                Entry("none", ("contain", "none")),
                Entry("content", ("contain", "content")),
                Entry("strict", ("contain", "strict")),
                Entry("size", ("--tw-contain-size", "size"), ("contain", "var(--tw-contain-size,) var(--tw-contain-layout,) var(--tw-contain-style,) var(--tw-contain-paint,)")),
                Entry("inline-size", ("--tw-contain-size", "inline-size"), ("contain", "var(--tw-contain-size,) var(--tw-contain-layout,) var(--tw-contain-style,) var(--tw-contain-paint,)")),
                Entry("layout", ("--tw-contain-layout", "layout"), ("contain", "var(--tw-contain-size,) var(--tw-contain-layout,) var(--tw-contain-style,) var(--tw-contain-paint,)")),
                Entry("style", ("--tw-contain-style", "style"), ("contain", "var(--tw-contain-size,) var(--tw-contain-layout,) var(--tw-contain-style,) var(--tw-contain-paint,)")),
                Entry("paint", ("--tw-contain-paint", "paint"), ("contain", "var(--tw-contain-size,) var(--tw-contain-layout,) var(--tw-contain-style,) var(--tw-contain-paint,)"))),
            Keyword("grid-flow", "Controls automatic grid placement.", 420, "grid-auto-flow", false,
                ("row", "row"), ("col", "column"), ("dense", "dense"), ("row-dense", "row dense"), ("col-dense", "column dense")),
            Keyword("auto-cols", "Sets automatically created grid column tracks.", 421, "grid-auto-columns", true,
                ("auto", "auto"), ("min", "min-content"), ("max", "max-content"), ("fr", "minmax(0, 1fr)")),
            Keyword("auto-rows", "Sets automatically created grid row tracks.", 422, "grid-auto-rows", true,
                ("auto", "auto"), ("min", "min-content"), ("max", "max-content"), ("fr", "minmax(0, 1fr)")),
            Keyword("caption", "Sets table caption placement.", 770, "caption-side", false,
                ("top", "top"), ("bottom", "bottom")),
            Keyword("whitespace", "Controls white-space collapsing and wrapping.", 730, "white-space", false,
                ("normal", "normal"), ("nowrap", "nowrap"), ("pre", "pre"), ("pre-line", "pre-line"),
                ("pre-wrap", "pre-wrap"), ("break-spaces", "break-spaces")),
            Keyword("hyphens", "Controls word hyphenation.", 731, null, false,
                Entry("none", ("-webkit-hyphens", "none"), ("hyphens", "none")),
                Entry("manual", ("-webkit-hyphens", "manual"), ("hyphens", "manual")),
                Entry("auto", ("-webkit-hyphens", "auto"), ("hyphens", "auto"))),
            Keyword("wrap", "Controls overflow word wrapping.", 732, "overflow-wrap", false,
                ("anywhere", "anywhere"), ("break-word", "break-word")),
            Keyword("mix-blend", "Sets element blend mode.", 900, "mix-blend-mode", false,
                BlendModes()),
            Keyword("bg-blend", "Sets background blend mode.", 901, "background-blend-mode", false,
                BlendModes()),
            Keyword("mask-composite", "Combines multiple mask layers.", 940, "mask-composite", false,
                ("add", "add"), ("subtract", "subtract"), ("intersect", "intersect"), ("exclude", "exclude")),
            Keyword("mask-mode", "Sets mask interpretation mode.", 941, "mask-mode", false,
                ("alpha", "alpha"), ("luminance", "luminance"), ("match", "match-source")),
            Keyword("mask-type", "Sets SVG mask type.", 942, "mask-type", false,
                ("alpha", "alpha"), ("luminance", "luminance")),
            Keyword("mask-size", "Sets mask image size.", 943, "mask-size", true,
                ("auto", "auto"), ("cover", "cover"), ("contain", "contain")),
            Keyword("mask-position", "Sets mask image position.", 944, "mask-position", true,
                ("top", "top"), ("top-left", "left top"), ("top-right", "right top"),
                ("bottom", "bottom"), ("bottom-left", "left bottom"), ("bottom-right", "right bottom"),
                ("left", "left"), ("right", "right"), ("center", "center")),
            Keyword("mask-repeat", "Sets mask image repetition.", 945, "mask-repeat", false,
                ("", "repeat"), ("no-repeat", "no-repeat"), ("x", "repeat-x"), ("y", "repeat-y"),
                ("round", "round"), ("space", "space")),
            Keyword("appearance", "Controls native platform styling.", 1500, "appearance", true,
                ("none", "none"), ("auto", "auto"), ("base-select", "base-select")),
            Keyword("field-sizing", "Controls form-field intrinsic sizing.", 1500, "field-sizing", false,
                ("content", "content"), ("fixed", "fixed")),
            Keyword("backface", "Controls backface visibility.", 1500, "backface-visibility", false,
                ("visible", "visible"), ("hidden", "hidden")),
            Keyword("scheme", "Sets the supported color scheme.", 1501, "color-scheme", false,
                ("normal", "normal"), ("dark", "dark"), ("light", "light"),
                ("light-dark", "light dark"), ("only-dark", "only dark"), ("only-light", "only light")),
            Keyword("cursor", "Sets the mouse cursor.", 1502, "cursor", true,
                CursorValues()),
            Keyword("resize", "Controls element resizing.", 1503, "resize", false,
                ("", "both"), ("none", "none"), ("x", "horizontal"), ("y", "vertical")),
            Keyword("scroll", "Controls scrolling behavior.", 1504, "scroll-behavior", false,
                ("auto", "auto"), ("smooth", "smooth")),
            Keyword("scrollbar", "Controls scrollbar width and gutter.", 1505, null, false,
                Entry("auto", ("scrollbar-width", "auto")),
                Entry("thin", ("scrollbar-width", "thin")),
                Entry("none", ("scrollbar-width", "none")),
                Entry("gutter-auto", ("scrollbar-gutter", "auto")),
                Entry("gutter-stable", ("scrollbar-gutter", "stable")),
                Entry("gutter-both", ("scrollbar-gutter", "stable both-edges"))),
            Keyword("snap", "Controls scroll snapping.", 1506, null, false,
                Entry("none", ("scroll-snap-type", "none")),
                Entry("x", ("scroll-snap-type", "x var(--tw-scroll-snap-strictness)")),
                Entry("y", ("scroll-snap-type", "y var(--tw-scroll-snap-strictness)")),
                Entry("both", ("scroll-snap-type", "both var(--tw-scroll-snap-strictness)")),
                Entry("mandatory", ("--tw-scroll-snap-strictness", "mandatory")),
                Entry("proximity", ("--tw-scroll-snap-strictness", "proximity")),
                Entry("align-none", ("scroll-snap-align", "none")),
                Entry("start", ("scroll-snap-align", "start")),
                Entry("end", ("scroll-snap-align", "end")),
                Entry("center", ("scroll-snap-align", "center")),
                Entry("normal", ("scroll-snap-stop", "normal")),
                Entry("always", ("scroll-snap-stop", "always"))),
            Keyword("touch", "Controls touch gesture behavior.", 1507, "touch-action", false,
                Entry("auto", ("touch-action", "auto")),
                Entry("none", ("touch-action", "none")),
                Entry("manipulation", ("touch-action", "manipulation")),
                Entry(
                    "pan-x",
                    ("--tw-pan-x", "pan-x"),
                    ("touch-action", "var(--tw-pan-x,) var(--tw-pan-y,) var(--tw-pinch-zoom,)")),
                Entry(
                    "pan-left",
                    ("--tw-pan-x", "pan-left"),
                    ("touch-action", "var(--tw-pan-x,) var(--tw-pan-y,) var(--tw-pinch-zoom,)")),
                Entry(
                    "pan-right",
                    ("--tw-pan-x", "pan-right"),
                    ("touch-action", "var(--tw-pan-x,) var(--tw-pan-y,) var(--tw-pinch-zoom,)")),
                Entry(
                    "pan-y",
                    ("--tw-pan-y", "pan-y"),
                    ("touch-action", "var(--tw-pan-x,) var(--tw-pan-y,) var(--tw-pinch-zoom,)")),
                Entry(
                    "pan-up",
                    ("--tw-pan-y", "pan-up"),
                    ("touch-action", "var(--tw-pan-x,) var(--tw-pan-y,) var(--tw-pinch-zoom,)")),
                Entry(
                    "pan-down",
                    ("--tw-pan-y", "pan-down"),
                    ("touch-action", "var(--tw-pan-x,) var(--tw-pan-y,) var(--tw-pinch-zoom,)")),
                Entry(
                    "pinch-zoom",
                    ("--tw-pinch-zoom", "pinch-zoom"),
                    ("touch-action", "var(--tw-pan-x,) var(--tw-pan-y,) var(--tw-pinch-zoom,)"))),
            Keyword("select", "Controls text selection.", 1508, null, false,
                Entry("none", ("-webkit-user-select", "none"), ("user-select", "none")),
                Entry("text", ("-webkit-user-select", "text"), ("user-select", "text")),
                Entry("all", ("-webkit-user-select", "all"), ("user-select", "all")),
                Entry("auto", ("-webkit-user-select", "auto"), ("user-select", "auto"))),
            Keyword("will-change", "Hints which property will change.", 1509, "will-change", true,
                ("auto", "auto"), ("scroll", "scroll-position"), ("contents", "contents"), ("transform", "transform")),
        };

    private static UtilityStaticDefinitionData Static(
        string candidate,
        string description,
        int order,
        params (string Property, string Value)[] declarations) =>
        new(
            candidate,
            description,
            order,
            declarations
                .Select(declaration =>
                    new UtilityCssDeclaration(
                        declaration.Property,
                        declaration.Value))
                .ToArray());

    private static UtilityKeywordDefinitionData Keyword(
        string root,
        string description,
        int order,
        string? property,
        bool supportsArbitraryValues,
        params (string Name, string Value)[] values) =>
        new(
            root,
            description,
            order,
            supportsArbitraryValues,
            property,
            values.Select(value =>
                Entry(
                    value.Name,
                    (property!, value.Value))),
            values.Select(value => root + (value.Name.Length == 0 ? string.Empty : "-" + value.Name))
                .ToArray());

    private static UtilityKeywordDefinitionData Keyword(
        string root,
        string description,
        int order,
        string? property,
        bool supportsArbitraryValues,
        params KeyValuePair<string, UtilityCssDeclaration[]>[] values) =>
        new(
            root,
            description,
            order,
            supportsArbitraryValues,
            property,
            values,
            values.Select(value => root + (value.Key.Length == 0 ? string.Empty : "-" + value.Key))
                .ToArray());

    private static KeyValuePair<string, UtilityCssDeclaration[]> Entry(
        string name,
        params (string Property, string Value)[] declarations) =>
        new(
            name,
            declarations
                .Select(declaration =>
                    new UtilityCssDeclaration(
                        declaration.Property,
                        declaration.Value))
                .ToArray());

    private static (string Name, string Value)[] BlendModes() =>
        new[]
        {
            ("normal", "normal"),
            ("multiply", "multiply"),
            ("screen", "screen"),
            ("overlay", "overlay"),
            ("darken", "darken"),
            ("lighten", "lighten"),
            ("color-dodge", "color-dodge"),
            ("color-burn", "color-burn"),
            ("hard-light", "hard-light"),
            ("soft-light", "soft-light"),
            ("difference", "difference"),
            ("exclusion", "exclusion"),
            ("hue", "hue"),
            ("saturation", "saturation"),
            ("color", "color"),
            ("luminosity", "luminosity"),
            ("plus-darker", "plus-darker"),
            ("plus-lighter", "plus-lighter"),
        };

    private static (string Name, string Value)[] CursorValues() =>
        new[]
        {
            ("auto", "auto"),
            ("default", "default"),
            ("pointer", "pointer"),
            ("wait", "wait"),
            ("text", "text"),
            ("move", "move"),
            ("help", "help"),
            ("not-allowed", "not-allowed"),
            ("none", "none"),
            ("context-menu", "context-menu"),
            ("progress", "progress"),
            ("cell", "cell"),
            ("crosshair", "crosshair"),
            ("vertical-text", "vertical-text"),
            ("alias", "alias"),
            ("copy", "copy"),
            ("no-drop", "no-drop"),
            ("grab", "grab"),
            ("grabbing", "grabbing"),
            ("all-scroll", "all-scroll"),
            ("col-resize", "col-resize"),
            ("row-resize", "row-resize"),
            ("n-resize", "n-resize"),
            ("e-resize", "e-resize"),
            ("s-resize", "s-resize"),
            ("w-resize", "w-resize"),
            ("ne-resize", "ne-resize"),
            ("nw-resize", "nw-resize"),
            ("se-resize", "se-resize"),
            ("sw-resize", "sw-resize"),
            ("ew-resize", "ew-resize"),
            ("ns-resize", "ns-resize"),
            ("nesw-resize", "nesw-resize"),
            ("nwse-resize", "nwse-resize"),
            ("zoom-in", "zoom-in"),
            ("zoom-out", "zoom-out"),
        };
}
