namespace Assimalign.Viu.Reactivity;

/// <summary>
/// A borrowed effect observation, passed by reference without boxing. Its identities must not
/// be retained after the synchronous callback. Specified by <c>[DVT-8]</c>.
/// </summary>
public readonly struct ReactivityInspectionEffect
{
    internal ReactivityInspectionEffect(ReactiveEffect effect, Dependency? cause, bool succeeded)
    {
        Effect = effect;
        Cause = cause;
        Succeeded = succeeded;
    }

    /// <summary>The observed effect's stable identity. Specified by <c>[DVT-8]</c>.</summary>
    public ReactiveEffect Effect { get; }
    /// <summary>The dependency responsible for scheduling; null for run boundaries. Specified by <c>[DVT-8]</c>.</summary>
    public Dependency? Cause { get; }
    /// <summary>Whether a completed run returned normally; false at other event boundaries. Specified by <c>[DVT-8]</c>.</summary>
    public bool Succeeded { get; }
}
