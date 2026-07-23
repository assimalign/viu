namespace Assimalign.Viu.Components;

/// <summary>Describes one platform attribute, property, or event binding.</summary>
public interface IComponentAttribute
{
    /// <summary>Gets the binding name.</summary>
    string Name { get; }

    /// <summary>Gets the binding value.</summary>
    object? Value { get; }
}

