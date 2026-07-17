using System.Globalization;
using System.Text;

namespace Assimalign.Vue.Shared;

/// <summary>
/// Diagnostics-only equivalent of upstream's <c>PatchFlagNames</c> map
/// (<c>packages/shared/src/patchFlags.ts</c>): renders <see cref="PatchFlags"/> values using the
/// upstream flag names (e.g. <c>NEED_HYDRATION</c>) for compiler codegen comments, devtools, and
/// error messages. Never call this on a hot path — it allocates. Implemented as switch-based
/// lookups (no dictionaries, no reflection, no static constructor) inside a standalone type, so
/// the trimmer removes the whole map from release WASM output whenever no diagnostic code
/// references it.
/// </summary>
public static class PatchFlagNames
{
    /// <summary>
    /// Formats a <see cref="PatchFlags"/> value using upstream flag names for diagnostic output.
    /// Single flags and the negative sentinels format as their upstream name (e.g.
    /// <c>"TEXT"</c>, <c>"CACHED"</c>, <c>"BAIL"</c>); combined positive flags format as a
    /// comma-separated list in ascending bit order (e.g. <c>"TEXT, CLASS"</c>), matching the
    /// upstream compiler's codegen comments. Values containing no known flag (including zero)
    /// format as their numeric value.
    /// </summary>
    /// <param name="flags">The patch flags to format.</param>
    /// <returns>A human-readable, upstream-parity name for <paramref name="flags"/>.</returns>
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
            for (var bit = 1; bit <= (int)PatchFlags.DevRootFragment; bit <<= 1)
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
        PatchFlags.Text => "TEXT",
        PatchFlags.Class => "CLASS",
        PatchFlags.Style => "STYLE",
        PatchFlags.Props => "PROPS",
        PatchFlags.FullProps => "FULL_PROPS",
        PatchFlags.NeedHydration => "NEED_HYDRATION",
        PatchFlags.StableFragment => "STABLE_FRAGMENT",
        PatchFlags.KeyedFragment => "KEYED_FRAGMENT",
        PatchFlags.UnkeyedFragment => "UNKEYED_FRAGMENT",
        PatchFlags.NeedPatch => "NEED_PATCH",
        PatchFlags.DynamicSlots => "DYNAMIC_SLOTS",
        PatchFlags.DevRootFragment => "DEV_ROOT_FRAGMENT",
        PatchFlags.Cached => "CACHED",
        PatchFlags.Bail => "BAIL",
        _ => null,
    };
}
