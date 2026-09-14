using System;
using System.Collections.Generic;
using System.Text.Json;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.DevTools;

// [DVT-13]: all assignment dispatch is statically typed; no reflection or JSON contract discovery.
internal static class StateValueEditor
{
    internal static bool TryEdit(
        object root,
        IReadOnlyList<string> path,
        JsonElement value,
        out string? reason)
    {
        reason = null;
        if (path.Count == 0 || path.Count > 64)
        {
            reason = "The edit path must contain between 1 and 64 segments.";
            return false;
        }

        try
        {
            object? parent = root;
            for (int index = 0; index < path.Count - 1; index++)
            {
                if (IsReadOnly(parent, out reason))
                {
                    return false;
                }

                if (!SnapshotValueEncoder.TryResolvePath(parent, [path[index]], out parent))
                {
                    reason = "The requested edit path no longer exists.";
                    return false;
                }
            }

            if (IsReadOnly(parent, out reason))
            {
                return false;
            }

            string member = path[path.Count - 1];
            if (parent is IReactiveReference && member == "value")
            {
                return TrySetReference(parent, value, out reason);
            }

            if (!SnapshotValueEncoder.TryResolvePath(parent, [member], out object? target))
            {
                reason = "The requested edit path no longer exists.";
                return false;
            }

            if (IsReadOnly(target, out reason))
            {
                return false;
            }

            if (target is IReactiveReference)
            {
                return TrySetReference(target, value, out reason);
            }

            if (parent is IReactiveObject reactiveObject)
            {
                if (!TryCoerce(value, target, out object? coerced))
                {
                    reason = "The JSON value does not match the supported scalar type or range.";
                    return false;
                }

                if (reactiveObject.TrySetMemberValue(member, coerced))
                {
                    return true;
                }
            }

            reason = "The target is not a writable scalar reactive reference or generated member.";
            return false;
        }
        catch
        {
            // User-defined getters/setters may throw; diagnostics must keep serving later requests.
            // A throwing setter may already have side effects, so no rollback is claimed.
            reason = "The state provider, getter, or setter failed; a setter may have changed state.";
            return false;
        }
    }

    private static bool IsReadOnly(object? target, out string? reason)
    {
        if (target is IReactiveReference { IsComputed: true })
        {
            reason = "Computed references are not editable.";
            return true;
        }

        if (target is IReactiveReadOnly { IsReadOnly: true })
        {
            reason = "The target is read-only.";
            return true;
        }

        reason = null;
        return false;
    }

    private static bool TrySetReference(object target, JsonElement value, out string? reason)
    {
        reason = null;
        if (IsReadOnly(target, out reason))
        {
            return false;
        }

        // The default values name the declared generic CLR type, even for a null string value.
        object? prototype = target switch
        {
            IReactiveReference<bool> => false,
            IReactiveReference<int> => 0,
            IReactiveReference<long> => 0L,
            IReactiveReference<double> => 0d,
            IReactiveReference<decimal> => 0m,
            IReactiveReference<string> => string.Empty,
            _ => null,
        };
        if (prototype is null)
        {
            reason = "The target is not a writable scalar reactive reference or generated member.";
            return false;
        }

        if (!TryCoerce(value, prototype, out object? coerced))
        {
            reason = "The JSON value does not match the supported scalar type or range.";
            return false;
        }

        switch (target)
        {
            case IReactiveReference<bool> reference: reference.Value = (bool)coerced!; break;
            case IReactiveReference<int> reference: reference.Value = (int)coerced!; break;
            case IReactiveReference<long> reference: reference.Value = (long)coerced!; break;
            case IReactiveReference<double> reference: reference.Value = (double)coerced!; break;
            case IReactiveReference<decimal> reference: reference.Value = (decimal)coerced!; break;
            case IReactiveReference<string> reference: reference.Value = (string)coerced!; break;
        }

        return true;
    }

    private static bool TryCoerce(JsonElement value, object? prototype, out object? result)
    {
        result = null;
        switch (prototype)
        {
            case bool when value.ValueKind is JsonValueKind.True or JsonValueKind.False:
                result = value.GetBoolean(); return true;
            case int when value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number):
                result = number; return true;
            case long when value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long number):
                result = number; return true;
            case double when value.ValueKind == JsonValueKind.Number
                && value.TryGetDouble(out double number) && double.IsFinite(number):
                result = number; return true;
            case decimal when value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out decimal number):
                result = number; return true;
            case null or string when value.ValueKind == JsonValueKind.String:
                result = value.GetString(); return true;
            case null or string when value.ValueKind == JsonValueKind.Null:
                return true;
            default:
                return false;
        }
    }
}
