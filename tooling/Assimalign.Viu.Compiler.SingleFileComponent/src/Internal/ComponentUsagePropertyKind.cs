namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>
/// How one property on a component usage in a template reaches the mounted component, which decides
/// whether it participates in build-time validation at all. Specified by <c>[SFC-USE-1]</c>.
/// </summary>
internal enum ComponentUsagePropertyKind
{
    /// <summary>
    /// A plain attribute (<c>title="x"</c>). Its value is a string literal, so it is the one form
    /// whose supplied type is always statically known.
    /// </summary>
    Static = 0,

    /// <summary>
    /// A bound value (<c>:title="expression"</c>, or a <c>v-model</c> target). Its type is known only
    /// when the expression is itself a C# literal.
    /// </summary>
    Bound = 1,

    /// <summary>
    /// A listener (<c>@click</c>, or an <c>onX</c>-spelled attribute). Never validated: an undeclared
    /// listener is a legitimate fallthrough attribute [CMP-17].
    /// </summary>
    Listener = 2,

    /// <summary>
    /// Any other directive (<c>v-if</c>, <c>v-for</c>, <c>v-slot</c>, a custom directive). Never a
    /// parameter, so never validated.
    /// </summary>
    Directive = 3,
}
