namespace Assimalign.Viu.State;

/// <summary>Selects an explicitly composed storage lifetime. Specified by <c>[STA-11]</c>.</summary>
public enum StateStorageKind
{
    /// <summary>Selects storage retained across browser sessions or its host-supplied equivalent.</summary>
    Local = 0,

    /// <summary>Selects browser page-session storage or its host-supplied equivalent.</summary>
    Session = 1,
}
