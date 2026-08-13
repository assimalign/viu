namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>
/// The attribute-declared component surface read out of one <c>@script</c> block ([CMP-26], [CMP-30]) —
/// the <c>[Parameter]</c> properties and <c>[Event]</c> methods the generator turns into the partial's
/// generated <c>ComponentContract.Parameters</c>/<c>Events</c> — plus the two emission facts the
/// scaffold needs
/// about the authored class body. Value-equatable, so it rides inside the incremental generator's
/// cached model without defeating the cache.
/// </summary>
/// <param name="Parameters">The declared input parameters, in declaration order.</param>
/// <param name="Events">The declared output events, in declaration order.</param>
/// <param name="DeclaresRequiredMember">
/// Whether any declared parameter used the C# <c>required</c> modifier. The scaffold then emits a
/// <c>[SetsRequiredMembers]</c> parameterless constructor so the AOT activator's <c>new T()</c> still
/// compiles while Viu keeps enforcing the requirement [CMP-28].
/// </param>
/// <param name="DeclaresConstructor">
/// Whether the authored block declares a constructor of its own, in which case the scaffold emits none.
/// </param>
public readonly record struct ScriptDeclarations(
    EquatableArray<ComponentParameterDeclaration> Parameters,
    EquatableArray<ComponentEventDeclaration> Events,
    bool DeclaresRequiredMember,
    bool DeclaresConstructor)
{
    /// <summary>The declarations of a block that declares no attributed component surface.</summary>
    public static readonly ScriptDeclarations None = default;

    /// <summary>Whether the block declared any attributed parameter or event.</summary>
    public bool IsEmpty => Parameters.Count == 0 && Events.Count == 0;

    /// <summary>
    /// Whether the authored block owns a <c>Parameters</c> member. Its values cannot be read statically,
    /// so component identity remains known while parameter-usage validation bails out ([SFC-USE-5]).
    /// Kept outside the primary constructor to preserve its existing constructor and deconstruction
    /// member shapes.
    /// </summary>
    public bool DeclaresImperativeParameters { get; init; }
}
