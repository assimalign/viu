using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Assimalign.Viu.Tooling.UtilityCss;

internal static class UtilityCustomPropertyCatalog
{
    private static readonly PropertyGroup[] Groups =
    {
        new(
            new[]
            {
                new PropertyRegistration("--tw-translate-x", "0"),
                new PropertyRegistration("--tw-translate-y", "0"),
                new PropertyRegistration("--tw-translate-z", "0"),
            }),
        new(
            new[]
            {
                new PropertyRegistration("--tw-scale-x", "1"),
                new PropertyRegistration("--tw-scale-y", "1"),
                new PropertyRegistration("--tw-scale-z", "1"),
            }),
        new(
            new[]
            {
                new PropertyRegistration("--tw-rotate-x"),
                new PropertyRegistration("--tw-rotate-y"),
                new PropertyRegistration("--tw-rotate-z"),
                new PropertyRegistration("--tw-skew-x"),
                new PropertyRegistration("--tw-skew-y"),
            }),
        new(
            new[]
            {
                new PropertyRegistration("--tw-outline-style", "solid"),
            }),
        new(
            new[]
            {
                new PropertyRegistration("--tw-leading"),
            }),
        new(
            new[]
            {
                new PropertyRegistration("--tw-duration"),
            }),
        new(
            new[]
            {
                new PropertyRegistration("--tw-ease"),
            }),
        new(
            new[]
            {
                new PropertyRegistration("--tw-pan-x"),
                new PropertyRegistration("--tw-pan-y"),
                new PropertyRegistration("--tw-pinch-zoom"),
            }),
        new(
            new[]
            {
                new PropertyRegistration("--tw-gradient-position"),
                new PropertyRegistration("--tw-gradient-from", "#0000", "<color>"),
                new PropertyRegistration("--tw-gradient-via", "#0000", "<color>"),
                new PropertyRegistration("--tw-gradient-to", "#0000", "<color>"),
                new PropertyRegistration("--tw-gradient-stops"),
                new PropertyRegistration("--tw-gradient-via-stops"),
                new PropertyRegistration(
                    "--tw-gradient-from-position",
                    "0%",
                    "<length-percentage>"),
                new PropertyRegistration(
                    "--tw-gradient-via-position",
                    "50%",
                    "<length-percentage>"),
                new PropertyRegistration(
                    "--tw-gradient-to-position",
                    "100%",
                    "<length-percentage>"),
            }),
        new(
            new[]
            {
                new PropertyRegistration(
                    "--tw-mask-linear",
                    "linear-gradient(#fff, #fff)"),
                new PropertyRegistration(
                    "--tw-mask-radial",
                    "linear-gradient(#fff, #fff)"),
                new PropertyRegistration(
                    "--tw-mask-conic",
                    "linear-gradient(#fff, #fff)"),
            }),
        new(
            new[]
            {
                new PropertyRegistration(
                    "--tw-mask-left",
                    "linear-gradient(#fff, #fff)"),
                new PropertyRegistration(
                    "--tw-mask-right",
                    "linear-gradient(#fff, #fff)"),
                new PropertyRegistration(
                    "--tw-mask-bottom",
                    "linear-gradient(#fff, #fff)"),
                new PropertyRegistration(
                    "--tw-mask-top",
                    "linear-gradient(#fff, #fff)"),
                new PropertyRegistration("--tw-mask-left-from-position", "0%"),
                new PropertyRegistration("--tw-mask-left-to-position", "100%"),
                new PropertyRegistration("--tw-mask-left-from-color", "black"),
                new PropertyRegistration("--tw-mask-left-to-color", "transparent"),
                new PropertyRegistration("--tw-mask-right-from-position", "0%"),
                new PropertyRegistration("--tw-mask-right-to-position", "100%"),
                new PropertyRegistration("--tw-mask-right-from-color", "black"),
                new PropertyRegistration("--tw-mask-right-to-color", "transparent"),
                new PropertyRegistration("--tw-mask-bottom-from-position", "0%"),
                new PropertyRegistration("--tw-mask-bottom-to-position", "100%"),
                new PropertyRegistration("--tw-mask-bottom-from-color", "black"),
                new PropertyRegistration("--tw-mask-bottom-to-color", "transparent"),
                new PropertyRegistration("--tw-mask-top-from-position", "0%"),
                new PropertyRegistration("--tw-mask-top-to-position", "100%"),
                new PropertyRegistration("--tw-mask-top-from-color", "black"),
                new PropertyRegistration("--tw-mask-top-to-color", "transparent"),
            }),
        new(
            new[]
            {
                new PropertyRegistration("--tw-mask-linear-position", "0deg"),
                new PropertyRegistration("--tw-mask-linear-from-position", "0%"),
                new PropertyRegistration("--tw-mask-linear-to-position", "100%"),
                new PropertyRegistration("--tw-mask-linear-from-color", "black"),
                new PropertyRegistration("--tw-mask-linear-to-color", "transparent"),
            }),
        new(
            new[]
            {
                new PropertyRegistration("--tw-mask-radial-from-position", "0%"),
                new PropertyRegistration("--tw-mask-radial-to-position", "100%"),
                new PropertyRegistration("--tw-mask-radial-from-color", "black"),
                new PropertyRegistration("--tw-mask-radial-to-color", "transparent"),
                new PropertyRegistration("--tw-mask-radial-shape", "ellipse"),
                new PropertyRegistration("--tw-mask-radial-size", "farthest-corner"),
                new PropertyRegistration("--tw-mask-radial-position", "center"),
            }),
        new(
            new[]
            {
                new PropertyRegistration("--tw-mask-conic-position", "0deg"),
                new PropertyRegistration("--tw-mask-conic-from-position", "0%"),
                new PropertyRegistration("--tw-mask-conic-to-position", "100%"),
                new PropertyRegistration("--tw-mask-conic-from-color", "black"),
                new PropertyRegistration("--tw-mask-conic-to-color", "transparent"),
            }),
        new(
            new[]
            {
                new PropertyRegistration("--tw-blur"),
                new PropertyRegistration("--tw-brightness"),
                new PropertyRegistration("--tw-contrast"),
                new PropertyRegistration("--tw-grayscale"),
                new PropertyRegistration("--tw-hue-rotate"),
                new PropertyRegistration("--tw-invert"),
                new PropertyRegistration("--tw-opacity"),
                new PropertyRegistration("--tw-saturate"),
                new PropertyRegistration("--tw-sepia"),
                new PropertyRegistration("--tw-drop-shadow"),
                new PropertyRegistration("--tw-drop-shadow-color"),
                new PropertyRegistration(
                    "--tw-drop-shadow-alpha",
                    "100%",
                    "<percentage>"),
                new PropertyRegistration("--tw-drop-shadow-size"),
            }),
        new(
            new[]
            {
                new PropertyRegistration("--tw-backdrop-blur"),
                new PropertyRegistration("--tw-backdrop-brightness"),
                new PropertyRegistration("--tw-backdrop-contrast"),
                new PropertyRegistration("--tw-backdrop-grayscale"),
                new PropertyRegistration("--tw-backdrop-hue-rotate"),
                new PropertyRegistration("--tw-backdrop-invert"),
                new PropertyRegistration("--tw-backdrop-opacity"),
                new PropertyRegistration("--tw-backdrop-saturate"),
                new PropertyRegistration("--tw-backdrop-sepia"),
            }),
        new(
            new[]
            {
                new PropertyRegistration("--tw-text-shadow-color"),
                new PropertyRegistration(
                    "--tw-text-shadow-alpha",
                    "100%",
                    "<percentage>"),
            }),
        new(
            new[]
            {
                new PropertyRegistration(
                    "--tw-border-style",
                    "solid"),
            }),
        new(
            new[]
            {
                new PropertyRegistration("--tw-shadow", "0 0 #0000"),
                new PropertyRegistration("--tw-shadow-color"),
                new PropertyRegistration(
                    "--tw-shadow-alpha",
                    "100%",
                    "<percentage>"),
                new PropertyRegistration("--tw-inset-shadow", "0 0 #0000"),
                new PropertyRegistration("--tw-inset-shadow-color"),
                new PropertyRegistration(
                    "--tw-inset-shadow-alpha",
                    "100%",
                    "<percentage>"),
                new PropertyRegistration("--tw-ring-color"),
                new PropertyRegistration("--tw-ring-shadow", "0 0 #0000"),
                new PropertyRegistration("--tw-inset-ring-color"),
                new PropertyRegistration(
                    "--tw-inset-ring-shadow",
                    "0 0 #0000"),
                new PropertyRegistration("--tw-ring-inset"),
                new PropertyRegistration(
                    "--tw-ring-offset-width",
                    "0px",
                    "<length>"),
                new PropertyRegistration("--tw-ring-offset-color", "#fff"),
                new PropertyRegistration(
                    "--tw-ring-offset-shadow",
                    "0 0 #0000"),
            }),
    };

    public static string EmitAll(CancellationToken cancellationToken) =>
        Emit(
            Groups,
            cancellationToken);

    public static string EmitNeeded(
        string usageCss,
        CancellationToken cancellationToken)
    {
        if (usageCss is null)
        {
            throw new ArgumentNullException(nameof(usageCss));
        }

        var groups = new List<PropertyGroup>();
        foreach (var group in Groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (group.IsUsedBy(usageCss))
            {
                groups.Add(group);
            }
        }

        return Emit(
            groups,
            cancellationToken);
    }

    private static string Emit(
        IReadOnlyList<PropertyGroup> groups,
        CancellationToken cancellationToken)
    {
        var result = new StringBuilder();
        foreach (var group in groups)
        {
            foreach (var registration in group.Registrations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                result.Append("@property ");
                result.Append(registration.Name);
                result.Append(" {\n");
                result.Append("  syntax: \"");
                result.Append(registration.Syntax);
                result.Append("\";\n");
                result.Append("  inherits: false;\n");
                if (registration.InitialValue is not null)
                {
                    result.Append("  initial-value: ");
                    result.Append(registration.InitialValue);
                    result.Append(";\n");
                }

                result.Append("}\n");
            }
        }

        return result.ToString();
    }

    private sealed class PropertyGroup
    {
        public PropertyGroup(
            IReadOnlyList<PropertyRegistration> registrations)
        {
            Registrations = registrations;
        }

        public IReadOnlyList<PropertyRegistration> Registrations { get; }

        public bool IsUsedBy(string css)
        {
            foreach (var registration in Registrations)
            {
                if (css.IndexOf(
                        registration.Name,
                        StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    private sealed class PropertyRegistration
    {
        public PropertyRegistration(
            string name,
            string? initialValue = null,
            string syntax = "*")
        {
            Name = name;
            InitialValue = initialValue;
            Syntax = syntax;
        }

        public string Name { get; }

        public string? InitialValue { get; }

        public string Syntax { get; }
    }
}
