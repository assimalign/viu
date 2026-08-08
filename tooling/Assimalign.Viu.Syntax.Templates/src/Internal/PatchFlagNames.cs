using System.Globalization;
using System.Text;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// Diagnostics-only rendering of <see cref="PatchFlags"/> values under their canonical
/// screaming-snake diagnostic names (e.g. <c>NEED_HYDRATION</c>), used in compiler code-generation
/// comments, devtools, and error messages. The names are a stable diagnostic vocabulary: tooling
/// reads them out of generated code, so they are renamed only alongside the tools that parse them.
/// Never call this on a hot path — it allocates. Implemented as switch-based
/// lookups (no dictionaries, no reflection, no static constructor) inside a standalone type, so
/// the trimmer removes the whole map from release WASM output whenever no diagnostic code
/// references it.
/// </summary>
internal static class PatchFlagNames
{
    /// <summary>
    /// Formats a <see cref="PatchFlags"/> value under its diagnostic name. Single flags and the
    /// negative sentinels format as that name (e.g. <c>"TEXT"</c>, <c>"CACHED"</c>,
    /// <c>"BAIL"</c>); combined positive flags format as a comma-separated list in ascending bit
    /// order (e.g. <c>"TEXT, CLASS"</c>), which is the form the compiler writes into its
    /// code-generation comments. Values containing no known flag (including zero) format as their
    /// numeric value.
    /// </summary>
    /// <param name="flags">The patch flags to format.</param>
    /// <returns>A human-readable diagnostic name for <paramref name="flags"/>.</returns>
    public static string Format(PatchFlags flags)
    {
        var single = GetName(flags);
        if (single is not null)
        {
            return single;
        }

        if (flags > 0)
        {
            StringBuilder? builder = null;
            for (var bit = 1; bit <= (int)PatchFlags.DevelopmentRootFragment; bit <<= 1)
            {
                var flag = (PatchFlags)bit;
                if ((flags & flag) == 0)
                {
                    continue;
                }

                builder ??= new StringBuilder();
                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(GetName(flag));
            }

            if (builder is not null)
            {
                return builder.ToString();
            }
        }

        return ((int)flags).ToString(CultureInfo.InvariantCulture);
    }

    private static string? GetName(PatchFlags flag) => flag switch
    {
        PatchFlags.None => "NONE",
        PatchFlags.Text => "TEXT",
        PatchFlags.Class => "CLASS",
        PatchFlags.Style => "STYLE",
        PatchFlags.Properties => "PROPS",
        PatchFlags.FullProperties => "FULL_PROPS",
        PatchFlags.NeedsHydration => "NEED_HYDRATION",
        PatchFlags.StableFragment => "STABLE_FRAGMENT",
        PatchFlags.KeyedFragment => "KEYED_FRAGMENT",
        PatchFlags.UnkeyedFragment => "UNKEYED_FRAGMENT",
        PatchFlags.NeedPatch => "NEED_PATCH",
        PatchFlags.DynamicSlots => "DYNAMIC_SLOTS",
        PatchFlags.DevelopmentRootFragment => "DEV_ROOT_FRAGMENT",
        PatchFlags.Cached => "CACHED",
        PatchFlags.Bail => "BAIL",
        _ => null,
    };
}
