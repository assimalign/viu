using System.Collections.Generic;

namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// The identifiers that expression rewriting leaves untouched: neither prefixed with the render context nor
/// reported as unresolved. The C# analogue of Vue 3.5's <c>isGloballyAllowed</c> plus the literal allow-list
/// used by <c>processExpression</c> (<c>@vue/compiler-core</c> <c>utils.ts</c>/<c>transformExpression.ts</c>,
/// backed by <c>@vue/shared</c>'s <c>GLOBALS_ALLOWED</c>).
/// </summary>
/// <remarks>
/// Vue's list names the JavaScript globals a template may reference (<c>Math</c>, <c>Date</c>, <c>JSON</c>,
/// <c>parseInt</c>, …). Because Vuecs expressions are C#, this is the corresponding common .NET base-class
/// surface a template legitimately reaches for. It is a Vuecs runtime-contract choice, documented in the
/// feature design notes; C# literal keywords (<c>true</c>, <c>false</c>, <c>null</c>, <c>this</c>) never reach
/// this check because Roslyn tokenizes them as keywords, not identifiers.
/// </remarks>
internal static class GlobalAllowList
{
    private static readonly HashSet<string> Allowed = new()
    {
        // Numeric and conversion helpers.
        "Math", "Convert", "Number", "BigInteger",
        "Byte", "SByte", "Int16", "UInt16", "Int32", "UInt32", "Int64", "UInt64",
        "Single", "Double", "Decimal", "Boolean", "Char",

        // Text.
        "String", "StringComparison", "StringComparer",

        // Time.
        "DateTime", "DateTimeOffset", "DateOnly", "TimeOnly", "TimeSpan", "DayOfWeek",

        // Collections and LINQ entry points a template expression commonly reaches for.
        "Array", "Enumerable", "Comparer", "EqualityComparer",

        // Identity and formatting.
        "Guid", "Uri", "Object", "Nullable",

        // Namespace roots for fully qualified access such as System.Math.PI.
        "System",

        // Runtime and culture surfaces.
        "Environment", "CultureInfo",
    };

    /// <summary>Whether <paramref name="name"/> is a global that must not be prefixed or reported.</summary>
    /// <param name="name">The identifier name.</param>
    public static bool IsAllowed(string name) => Allowed.Contains(name);
}
