namespace Assimalign.Viu.Reactivity;

/// <summary>
/// A borrowed dependency observation passed by reference without boxing. Identities and owner
/// objects remain valid only during the callback; a recorder must copy scalar metadata and use
/// weak identities. Specified by <c>[DVT-8]</c> and <c>[DVT-9]</c>.
/// </summary>
public readonly struct ReactivityInspectionDependency
{
    internal ReactivityInspectionDependency(Dependency dependency, Subscriber? subscriber,
        object? owner, object? memberKey, string? debugLabel, bool isWrite)
    {
        Dependency = dependency;
        Subscriber = subscriber;
        Owner = owner;
        MemberKey = memberKey;
        DebugLabel = debugLabel;
        Version = dependency.Version;
        IsWrite = isWrite;
    }

    /// <summary>The dependency's stable identity. Specified by <c>[DVT-9]</c>.</summary>
    public Dependency Dependency { get; }
    /// <summary>The ambient reading or writing subscriber, when present. Specified by <c>[DVT-8]</c>.</summary>
    public Subscriber? Subscriber { get; }
    /// <summary>The attributed owner, if still alive; never retained by the seam. Specified by <c>[DVT-9]</c>.</summary>
    public object? Owner { get; }
    /// <summary>The generated property name or keyed member identity, when known. Specified by <c>[DVT-9]</c>.</summary>
    public object? MemberKey { get; }
    /// <summary>The caller's optional reference label; unnamed cells use recorder identities. Specified by <c>[DVT-9]</c>.</summary>
    public string? DebugLabel { get; }
    /// <summary>The dependency version at observation time. Specified by <c>[DVT-8]</c>.</summary>
    public int Version { get; }
    /// <summary>Whether a trigger represents a state write, rather than computed propagation. Specified by <c>[DVT-8]</c>.</summary>
    public bool IsWrite { get; }
}
