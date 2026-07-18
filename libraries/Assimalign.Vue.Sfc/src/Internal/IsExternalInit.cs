namespace System.Runtime.CompilerServices;

/// <summary>
/// Compiler shim enabling <c>init</c> accessors and <c>record</c> types on <c>netstandard2.0</c>, where
/// the runtime does not ship <c>IsExternalInit</c>. The block models (see the
/// <c>Assimalign.Vue.Sfc</c> namespace) rely on this. Referenced only by the C# compiler; never used at
/// run time. Mirrors the shim in <c>Assimalign.Vue.Compiler</c>.
/// </summary>
internal static class IsExternalInit
{
}
