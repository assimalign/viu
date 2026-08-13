namespace System.Runtime.CompilerServices;

/// <summary>
/// Compiler shim enabling <c>init</c> accessors and <c>record</c> types on <c>netstandard2.0</c>,
/// where the runtime does not ship <c>IsExternalInit</c>. The immutable node records of
/// <c>Assimalign.Viu.Syntax</c> and its derived parsers rely on this. Referenced only by the C#
/// compiler; never used at run time. This is the single authoritative copy: it is compiled here and
/// <c>&lt;Compile Include&gt;</c>-linked (still <c>internal</c>) into the derived parser projects, which
/// each need their own copy because an <c>internal</c> shim in a referenced assembly is not visible to
/// the referencing compilation.
/// </summary>
internal static class IsExternalInit
{
}
