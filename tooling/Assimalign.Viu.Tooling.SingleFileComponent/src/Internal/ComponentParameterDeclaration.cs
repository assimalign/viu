namespace Assimalign.Viu.Tooling.SingleFileComponent;

/// <summary>
/// One <c>[Parameter]</c>-declared component input read out of an <c>@script</c> block: the canonical
/// template-facing name, the property that receives the argument, and the declared C# type. The
/// generator emits the equivalent <c>ComponentParameter</c> plus the per-render assignment from it, and
/// the same record is what makes a component's input surface readable at build time by a
/// <em>consumer's</em> template ([SFC-USE-1]). Specified by <c>[CMP-26]</c>.
/// A <see langword="readonly"/> <see langword="record"/> <see langword="struct"/> so it rides inside the
/// incremental generator's cached model without defeating the cache.
/// </summary>
/// <param name="Name">The canonical argument name, derived from <paramref name="PropertyName"/> or set explicitly [CMP-27].</param>
/// <param name="PropertyName">The declaring property's C# name.</param>
/// <param name="TypeText">The property's declared type exactly as spelled in the <c>@script</c> block.</param>
/// <param name="TypeKind">How much of <paramref name="TypeText"/> the build-time checker can decide.</param>
/// <param name="IsRequired">Whether the caller must supply the parameter [CMP-28].</param>
/// <param name="IsRequiredMember">Whether the C# <c>required</c> modifier declared the requiredness.</param>
internal readonly record struct ComponentParameterDeclaration(
    string Name,
    string PropertyName,
    string TypeText,
    ComponentValueKind TypeKind,
    bool IsRequired,
    bool IsRequiredMember);
