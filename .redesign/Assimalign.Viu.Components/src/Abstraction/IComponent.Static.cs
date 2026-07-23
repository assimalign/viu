namespace Assimalign.Viu.Components;

/// <summary>Describes a pre-rendered static content range.</summary>
public interface IStaticComponent : IComponent
{
    /// <summary>Gets the platform-specific static markup.</summary>
    string Content { get; }
}

