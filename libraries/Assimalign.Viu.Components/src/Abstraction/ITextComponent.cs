namespace Assimalign.Viu.Components;

/// <summary>Describes a text value in the component tree.</summary>
public interface ITextComponent : IComponent
{
    /// <summary>Gets the text content.</summary>
    string Text { get; }
}
