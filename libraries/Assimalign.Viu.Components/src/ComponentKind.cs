namespace Assimalign.Viu.Components;

/// <summary>Discriminates the specialized values in the component tree.</summary>
public enum ComponentKind
{
    /// <summary>A platform element.</summary>
    Element,

    /// <summary>A user-authored template component request.</summary>
    Template,

    /// <summary>A text value.</summary>
    Text,

    /// <summary>A comment or empty placeholder.</summary>
    Comment,

    /// <summary>A pre-rendered static range.</summary>
    Static,

    /// <summary>A group of siblings.</summary>
    Fragment,

    /// <summary>Content rendered into a different platform container.</summary>
    Teleport,
}

