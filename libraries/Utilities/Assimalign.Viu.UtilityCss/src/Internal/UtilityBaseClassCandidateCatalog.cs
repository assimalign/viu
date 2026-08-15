using System;
using System.Collections.Generic;
using System.Globalization;

namespace Assimalign.Viu.UtilityCss;

internal static class UtilityBaseClassCandidateCatalog
{
    // UtilityDefinition.CompletionCandidates intentionally stays representative for live editor
    // queries. The build catalog needs the exhaustive finite named vocabulary instead; its caller
    // combines these candidates with theme tokens and resolves every result through the engine.
    private static readonly string[] CommonFractions =
    {
        "1/2",
        "1/3",
        "2/3",
        "1/4",
        "2/4",
        "3/4",
        "1/5",
        "2/5",
        "3/5",
        "4/5",
        "1/6",
        "2/6",
        "3/6",
        "4/6",
        "5/6",
        "1/12",
        "2/12",
        "3/12",
        "4/12",
        "5/12",
        "6/12",
        "7/12",
        "8/12",
        "9/12",
        "10/12",
        "11/12",
    };

    private static readonly string[] SizeValues =
    {
        "auto",
        "full",
        "screen",
        "svw",
        "lvw",
        "dvw",
        "svh",
        "lvh",
        "dvh",
        "lh",
        "min",
        "max",
        "fit",
        "none",
    };

    private static readonly string[] PositionValues =
    {
        "center",
        "top",
        "top-right",
        "right",
        "bottom-right",
        "bottom",
        "bottom-left",
        "left",
        "top-left",
    };

    private static readonly string[] PercentageScale =
    {
        "0",
        "5",
        "10",
        "15",
        "20",
        "25",
        "30",
        "35",
        "40",
        "45",
        "50",
        "55",
        "60",
        "65",
        "70",
        "75",
        "80",
        "85",
        "90",
        "95",
        "100",
    };

    private static readonly string[] RotationAngleScale =
    {
        "0",
        "1",
        "2",
        "3",
        "6",
        "12",
        "45",
        "90",
        "180",
    };

    private static readonly string[] SkewAngleScale =
    {
        "0",
        "1",
        "2",
        "3",
        "6",
        "12",
    };

    private static readonly string[] GradientAngleScale =
    {
        "0",
        "30",
        "60",
        "90",
        "120",
        "150",
        "180",
        "210",
        "240",
        "270",
        "300",
        "330",
    };

    public static void AddCandidates(
        ISet<string> candidates,
        UtilityRegisteredDefinition registration)
    {
        var root = registration.Definition.Root;
        switch (registration.ResolverKind)
        {
            case UtilityResolverKind.Keyword:
                if (root == "object")
                {
                    AddValues(
                        candidates,
                        root,
                        new[]
                        {
                            "top-left",
                            "top-right",
                            "bottom-left",
                            "bottom-right",
                        });
                }

                break;
            case UtilityResolverKind.Display:
                AddDisplayCandidates(candidates, root);
                break;
            case UtilityResolverKind.PositionOffset:
                AddValues(candidates, root, new[] { "auto", "full" });
                AddValues(candidates, root, CommonFractions);
                break;
            case UtilityResolverKind.Integer:
                AddIntegerCandidates(candidates, root);
                break;
            case UtilityResolverKind.AspectRatio:
                AddValues(candidates, root, new[] { "auto", "square", "video" });
                AddValues(candidates, root, CommonFractions);
                break;
            case UtilityResolverKind.Columns:
                AddValues(
                    candidates,
                    root,
                    new[]
                    {
                        "auto",
                        "3xs",
                        "2xs",
                        "xs",
                        "sm",
                        "md",
                        "lg",
                        "xl",
                        "2xl",
                        "3xl",
                        "4xl",
                        "5xl",
                        "6xl",
                        "7xl",
                    });
                AddIntegerRange(candidates, root, 1, 12);
                break;
            case UtilityResolverKind.Flex:
                if (root == "flex")
                {
                    AddValues(
                        candidates,
                        root,
                        new[]
                        {
                            "1",
                            "auto",
                            "initial",
                            "none",
                            "wrap",
                            "nowrap",
                            "wrap-reverse",
                        });
                    AddIntegerRange(candidates, root, 1, 12);
                    AddValues(candidates, root, CommonFractions);
                }

                break;
            case UtilityResolverKind.FlexBasis:
                AddSizeCandidates(candidates, root);
                break;
            case UtilityResolverKind.Container:
                candidates.Add(root);
                AddValues(candidates, root, new[] { "normal", "size" });
                break;
            case UtilityResolverKind.Alignment:
                AddValues(
                    candidates,
                    root,
                    new[]
                    {
                        "start",
                        "end",
                        "center",
                        "between",
                        "around",
                        "evenly",
                        "baseline",
                        "stretch",
                        "auto",
                        "normal",
                    });
                break;
            case UtilityResolverKind.GridColumns:
            case UtilityResolverKind.GridRows:
                AddValues(candidates, root, new[] { "none", "subgrid" });
                AddIntegerRange(candidates, root, 1, 12);
                break;
            case UtilityResolverKind.GridSpan:
                AddValues(candidates, root, new[] { "full" });
                AddIntegerRange(candidates, root, 1, 12);
                break;
            case UtilityResolverKind.GridLine:
                AddValues(candidates, root, new[] { "auto" });
                AddIntegerRange(candidates, root, 1, 13);
                break;
            case UtilityResolverKind.Spacing:
                if (root is
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
                    "mbe")
                {
                    AddValues(candidates, root, new[] { "auto" });
                }

                break;
            case UtilityResolverKind.Divide:
                if (root == "divide")
                {
                    AddValues(
                        candidates,
                        root,
                        new[] { "solid", "dashed", "dotted", "double", "none" });
                }
                else
                {
                    candidates.Add(root);
                    AddValues(candidates, root, new[] { "0", "2", "4", "8" });
                }

                break;
            case UtilityResolverKind.Sizing:
                AddSizeCandidates(candidates, root);
                break;
            case UtilityResolverKind.Text:
                AddValues(
                    candidates,
                    root,
                    new[]
                    {
                        "left",
                        "center",
                        "right",
                        "justify",
                        "start",
                        "end",
                        "ellipsis",
                        "clip",
                        "wrap",
                        "nowrap",
                        "balance",
                        "pretty",
                    });
                break;
            case UtilityResolverKind.Typography:
                AddTypographyCandidates(candidates, root);
                break;
            case UtilityResolverKind.Background:
                AddBackgroundCandidates(candidates, root);
                break;
            case UtilityResolverKind.Border:
                candidates.Add(root);
                AddValues(
                    candidates,
                    root,
                    new[]
                    {
                        "0",
                        "2",
                        "4",
                        "8",
                        "solid",
                        "dashed",
                        "dotted",
                        "double",
                        "hidden",
                        "none",
                    });
                break;
            case UtilityResolverKind.Outline:
                candidates.Add(root);
                AddValues(
                    candidates,
                    root,
                    new[]
                    {
                        "0",
                        "1",
                        "2",
                        "4",
                        "8",
                        "none",
                        "hidden",
                        "solid",
                        "dashed",
                        "dotted",
                        "double",
                    });
                break;
            case UtilityResolverKind.Radius:
                candidates.Add(root);
                AddValues(candidates, root, new[] { "none", "full" });
                break;
            case UtilityResolverKind.Opacity:
                AddValues(candidates, root, PercentageScale);
                break;
            case UtilityResolverKind.Shadow:
                candidates.Add(root);
                AddValues(
                    candidates,
                    root,
                    new[] { "2xs", "xs", "sm", "md", "lg", "xl", "2xl", "none" });
                break;
            case UtilityResolverKind.Ring:
                if (root is "ring" or "inset-ring")
                {
                    candidates.Add(root);
                }

                AddValues(candidates, root, new[] { "0", "1", "2", "4", "8" });
                break;
            case UtilityResolverKind.Mask:
                AddMaskCandidates(candidates, root);
                break;
            case UtilityResolverKind.Filter:
            case UtilityResolverKind.BackdropFilter:
                AddFilterCandidates(candidates, root);
                break;
            case UtilityResolverKind.Transition:
                AddTransitionCandidates(candidates, root);
                break;
            case UtilityResolverKind.Transform:
                AddTransformCandidates(candidates, root);
                break;
            case UtilityResolverKind.Color:
                if (root == "accent")
                {
                    AddValues(candidates, root, new[] { "auto" });
                }

                break;
            case UtilityResolverKind.Svg:
                AddValues(candidates, root, new[] { "none", "current" });
                if (root == "stroke")
                {
                    AddValues(candidates, root, new[] { "0", "1", "2" });
                }

                break;
        }
    }

    private static void AddDisplayCandidates(
        ISet<string> candidates,
        string root)
    {
        if (root is "inline" or "block")
        {
            AddSizeCandidates(candidates, root);
        }

        if (root == "table")
        {
            AddValues(
                candidates,
                root,
                new[]
                {
                    "auto",
                    "fixed",
                    "caption",
                    "cell",
                    "column",
                    "column-group",
                    "footer-group",
                    "header-group",
                    "row-group",
                    "row",
                });
        }
    }

    private static void AddIntegerCandidates(
        ISet<string> candidates,
        string root)
    {
        switch (root)
        {
            case "z":
                AddValues(
                    candidates,
                    root,
                    new[] { "auto", "0", "10", "20", "30", "40", "50" });
                break;
            case "order":
                AddValues(candidates, root, new[] { "first", "last", "none" });
                AddIntegerRange(candidates, root, 0, 12);
                break;
            case "tab":
                AddValues(candidates, root, new[] { "1", "2", "4", "8" });
                break;
            case "zoom":
                AddValues(
                    candidates,
                    root,
                    new[]
                    {
                        "50",
                        "75",
                        "90",
                        "95",
                        "100",
                        "105",
                        "110",
                        "125",
                        "150",
                        "200",
                    });
                break;
        }
    }

    private static void AddSizeCandidates(
        ISet<string> candidates,
        string root)
    {
        AddValues(candidates, root, SizeValues);
        AddValues(candidates, root, CommonFractions);
    }

    private static void AddTypographyCandidates(
        ISet<string> candidates,
        string root)
    {
        switch (root)
        {
            case "leading":
                AddValues(
                    candidates,
                    root,
                    new[] { "none", "tight", "snug", "normal", "relaxed", "loose" });
                break;
            case "tracking":
                AddValues(
                    candidates,
                    root,
                    new[] { "tighter", "tight", "normal", "wide", "wider", "widest" });
                break;
            case "line-clamp":
                AddValues(candidates, root, new[] { "none" });
                AddIntegerRange(candidates, root, 1, 6);
                break;
            case "align":
                AddValues(
                    candidates,
                    root,
                    new[]
                    {
                        "baseline",
                        "top",
                        "middle",
                        "bottom",
                        "text-top",
                        "text-bottom",
                        "sub",
                        "super",
                    });
                break;
            case "list":
                AddValues(
                    candidates,
                    root,
                    new[] { "inside", "outside", "none", "disc", "decimal" });
                break;
            case "list-image":
                AddValues(candidates, root, new[] { "none" });
                break;
            case "decoration":
                AddValues(
                    candidates,
                    root,
                    new[]
                    {
                        "solid",
                        "double",
                        "dotted",
                        "dashed",
                        "wavy",
                        "auto",
                        "from-font",
                        "0",
                        "1",
                        "2",
                        "4",
                        "8",
                    });
                break;
            case "underline-offset":
                AddValues(candidates, root, new[] { "auto", "0", "1", "2", "4", "8" });
                break;
            case "font-stretch":
                AddValues(
                    candidates,
                    root,
                    new[]
                    {
                        "ultra-condensed",
                        "extra-condensed",
                        "condensed",
                        "semi-condensed",
                        "normal",
                        "semi-expanded",
                        "expanded",
                        "extra-expanded",
                        "ultra-expanded",
                        "50%",
                        "75%",
                        "90%",
                        "95%",
                        "100%",
                        "105%",
                        "110%",
                        "125%",
                        "150%",
                        "200%",
                    });
                break;
        }
    }

    private static void AddBackgroundCandidates(
        ISet<string> candidates,
        string root)
    {
        switch (root)
        {
            case "bg":
                AddValues(
                    candidates,
                    root,
                    new[]
                    {
                        "fixed",
                        "local",
                        "scroll",
                        "clip-border",
                        "clip-padding",
                        "clip-content",
                        "clip-text",
                        "origin-border",
                        "origin-padding",
                        "origin-content",
                        "repeat",
                        "no-repeat",
                        "repeat-x",
                        "repeat-y",
                        "repeat-round",
                        "repeat-space",
                        "auto",
                        "cover",
                        "contain",
                        "bottom",
                        "center",
                        "left",
                        "left-bottom",
                        "left-top",
                        "right",
                        "right-bottom",
                        "right-top",
                        "top",
                        "none",
                    });
                break;
            case "bg-linear":
                AddValues(
                    candidates,
                    root,
                    new[]
                    {
                        "to-t",
                        "to-tr",
                        "to-r",
                        "to-br",
                        "to-b",
                        "to-bl",
                        "to-l",
                        "to-tl",
                    });
                AddValues(candidates, root, GradientAngleScale);
                break;
            case "bg-conic":
                candidates.Add(root);
                AddValues(candidates, root, GradientAngleScale);
                break;
            case "bg-radial":
                candidates.Add(root);
                break;
            case "from":
            case "via":
            case "to":
                AddPercentageCandidates(candidates, root);
                break;
        }
    }

    private static void AddMaskCandidates(
        ISet<string> candidates,
        string root)
    {
        switch (root)
        {
            case "mask":
                AddValues(
                    candidates,
                    root,
                    new[]
                    {
                        "none",
                        "add",
                        "subtract",
                        "intersect",
                        "exclude",
                        "alpha",
                        "luminance",
                        "match",
                        "type-alpha",
                        "type-luminance",
                        "auto",
                        "cover",
                        "contain",
                        "top",
                        "top-left",
                        "top-right",
                        "bottom",
                        "bottom-left",
                        "bottom-right",
                        "left",
                        "right",
                        "center",
                        "repeat",
                        "no-repeat",
                        "repeat-x",
                        "repeat-y",
                        "repeat-round",
                        "repeat-space",
                        "circle",
                        "ellipse",
                        "radial-closest-side",
                        "radial-farthest-side",
                        "radial-closest-corner",
                        "radial-farthest-corner",
                    });
                break;
            case "mask-radial":
                AddValues(
                    candidates,
                    root,
                    new[]
                    {
                        "closest-side",
                        "farthest-side",
                        "closest-corner",
                        "farthest-corner",
                    });
                break;
            case "mask-radial-at":
                AddValues(candidates, root, PositionValues);
                break;
            case "mask-linear":
            case "mask-conic":
                AddValues(candidates, root, RotationAngleScale);
                break;
            default:
                AddPercentageCandidates(candidates, root);
                break;
        }
    }

    private static void AddFilterCandidates(
        ISet<string> candidates,
        string root)
    {
        var plainRoot = root.StartsWith("backdrop-", StringComparison.Ordinal)
            ? root.Substring("backdrop-".Length)
            : root;
        switch (plainRoot)
        {
            case "filter":
                candidates.Add(root);
                AddValues(candidates, root, new[] { "none" });
                break;
            case "blur":
                candidates.Add(root);
                AddValues(
                    candidates,
                    root,
                    new[] { "none", "xs", "sm", "md", "lg", "xl", "2xl", "3xl" });
                break;
            case "brightness":
                AddValues(
                    candidates,
                    root,
                    new[] { "0", "50", "75", "90", "95", "100", "105", "110", "125", "150", "200" });
                break;
            case "contrast":
                AddValues(
                    candidates,
                    root,
                    new[] { "0", "50", "75", "100", "125", "150", "200" });
                break;
            case "drop-shadow":
                candidates.Add(root);
                AddValues(
                    candidates,
                    root,
                    new[] { "xs", "sm", "md", "lg", "xl", "2xl", "none" });
                break;
            case "grayscale":
            case "invert":
                candidates.Add(root);
                AddValues(candidates, root, new[] { "0", "25", "50", "75", "100" });
                break;
            case "sepia":
                candidates.Add(root);
                AddValues(candidates, root, new[] { "0", "50", "100" });
                break;
            case "opacity":
                AddValues(candidates, root, PercentageScale);
                break;
            case "saturate":
                AddValues(candidates, root, new[] { "0", "50", "100", "150", "200" });
                break;
            case "hue-rotate":
                AddValues(candidates, root, new[] { "0", "15", "30", "60", "90", "180" });
                break;
        }
    }

    private static void AddTransitionCandidates(
        ISet<string> candidates,
        string root)
    {
        switch (root)
        {
            case "transition":
                candidates.Add(root);
                AddValues(
                    candidates,
                    root,
                    new[]
                    {
                        "all",
                        "colors",
                        "opacity",
                        "shadow",
                        "transform",
                        "none",
                        "discrete",
                        "normal",
                    });
                break;
            case "duration":
            case "delay":
                AddValues(
                    candidates,
                    root,
                    new[] { "0", "75", "100", "150", "200", "300", "500", "700", "1000" });
                break;
            case "ease":
                AddValues(candidates, root, new[] { "linear", "in", "out", "in-out", "initial" });
                break;
            case "animate":
                AddValues(candidates, root, new[] { "none" });
                break;
        }
    }

    private static void AddTransformCandidates(
        ISet<string> candidates,
        string root)
    {
        if (root.StartsWith("translate", StringComparison.Ordinal))
        {
            AddSizeCandidates(candidates, root);
            return;
        }

        if (root.StartsWith("rotate", StringComparison.Ordinal))
        {
            AddValues(candidates, root, RotationAngleScale);
            return;
        }

        if (root.StartsWith("skew", StringComparison.Ordinal))
        {
            AddValues(candidates, root, SkewAngleScale);
            return;
        }

        if (root.StartsWith("scale", StringComparison.Ordinal))
        {
            AddValues(
                candidates,
                root,
                new[]
                {
                    "0",
                    "50",
                    "75",
                    "90",
                    "95",
                    "100",
                    "105",
                    "110",
                    "125",
                    "150",
                    "200",
                });
            return;
        }

        if (root is "origin" or "perspective-origin")
        {
            AddValues(candidates, root, PositionValues);
            return;
        }

        if (root == "perspective")
        {
            AddValues(
                candidates,
                root,
                new[] { "none", "dramatic", "near", "normal", "midrange", "distant" });
            return;
        }

        if (root == "transform")
        {
            candidates.Add(root);
            AddValues(
                candidates,
                root,
                new[]
                {
                    "none",
                    "gpu",
                    "cpu",
                    "flat",
                    "3d",
                    "content",
                    "border",
                    "fill",
                    "stroke",
                    "view",
                });
        }
    }

    private static void AddPercentageCandidates(
        ISet<string> candidates,
        string root)
    {
        foreach (var percentage in PercentageScale)
        {
            candidates.Add(root + "-" + percentage + "%");
        }
    }

    private static void AddIntegerRange(
        ISet<string> candidates,
        string root,
        int first,
        int last)
    {
        for (var value = first; value <= last; value++)
        {
            candidates.Add(
                root + "-" + value.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static void AddValues(
        ISet<string> candidates,
        string root,
        IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            candidates.Add(root + "-" + value);
        }
    }
}
