namespace Assimalign.Viu;

/// <summary>Classifies one applied component metadata update.</summary>
internal enum ComponentHotReloadChangeKind
{
    None,
    StyleOnly,
    Template,
    ScriptReset,
}
