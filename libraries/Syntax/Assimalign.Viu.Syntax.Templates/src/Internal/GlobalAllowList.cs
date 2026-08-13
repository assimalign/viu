using System.Collections.Generic;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The identifiers that expression rewriting leaves untouched: neither prefixed with the render context nor
/// reported as unresolved.
/// </summary>
/// <remarks>
/// Because template expressions are C#, the list is the common .NET base-class surface a template
/// legitimately reaches for (<c>Math</c>, <c>String</c>, <c>Convert</c>, …). Membership is a deliberate
/// contract, not a convenience: a name on this list can never be shadowed by a component member, so
/// adding one is a breaking change for any component that already declares that member. C# literal
/// keywords (<c>true</c>, <c>false</c>, <c>null</c>, <c>this</c>) never reach this check because Roslyn
/// tokenizes them as keywords, not identifiers.
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
