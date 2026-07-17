namespace System.Runtime.CompilerServices;

/// <summary>
/// Compiler shim enabling <c>init</c> accessors and <c>record</c> types on <c>netstandard2.0</c>,
/// where the runtime does not ship <c>IsExternalInit</c>. Referenced only by the compiler; never
/// used at run time.
/// </summary>
internal static class IsExternalInit
{
}
