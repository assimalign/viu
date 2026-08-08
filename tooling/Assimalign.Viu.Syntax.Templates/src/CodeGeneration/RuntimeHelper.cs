namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// A symbolic operation carried by the template transform intermediate representation, such as block
/// creation, list rendering, or an event modifier guard.
/// </summary>
/// <remarks>
/// The compiler does not reference runtime assemblies. The render writer recognizes each identity and
/// lowers it to the adopted direct API or language construct; <see cref="Name"/> is an internal IR key,
/// not a member emitted for runtime lookup. Two operations with the same name are equal so transforms
/// can register requirements independently. Specified by <c>[SFC-CG-2]</c>.
/// </remarks>
/// <param name="Name">The canonical intermediate-operation name.</param>
public sealed record RuntimeHelper(string Name);
