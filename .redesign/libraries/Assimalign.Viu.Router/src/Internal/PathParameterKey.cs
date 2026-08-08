namespace Assimalign.Viu.Router;

/// <summary>
/// Describes one capturing parameter in a compiled path pattern — its name and the modifiers that
/// govern how its captured value is read back. Keys are stored in pattern order, so the i-th
/// capturing group of the compiled expression maps to the i-th key.
/// </summary>
internal readonly struct PathParameterKey
{
    /// <summary>Creates a parameter key.</summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="repeatable">Whether the parameter is repeatable (its captured value splits on <c>/</c>).</param>
    /// <param name="optional">Whether the parameter is optional (its capturing group may not participate).</param>
    public PathParameterKey(string name, bool repeatable, bool optional)
    {
        Name = name;
        Repeatable = repeatable;
        Optional = optional;
    }

    /// <summary>The parameter name.</summary>
    public string Name { get; }

    /// <summary>Whether the captured value should be split on <c>/</c> into multiple values.</summary>
    public bool Repeatable { get; }

    /// <summary>Whether the parameter is optional.</summary>
    public bool Optional { get; }
}
