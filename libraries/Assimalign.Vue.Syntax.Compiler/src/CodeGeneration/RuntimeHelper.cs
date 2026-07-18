namespace Assimalign.Vue.Syntax.Compiler;

/// <summary>
/// A by-name reference to a runtime helper the generated render function will import from the runtime
/// (e.g. <c>createElementBlock</c>, <c>renderList</c>, <c>withModifiers</c>). The C# port of the
/// <c>symbol</c> helper identities in Vue 3.5's <c>@vue/compiler-core</c> <c>runtimeHelpers.ts</c> and
/// <c>@vue/compiler-dom</c> <c>runtimeHelpers.ts</c>.
/// </summary>
/// <remarks>
/// The compiler NEVER references the runtime assembly: helpers are carried as plain names and resolved to
/// concrete symbols by code generation ([V01.01.05.05]) and the runtime ([V01.01.05.02]). Two helpers with
/// the same <see cref="Name"/> are equal, mirroring upstream's shared symbol identity; the canonical names
/// live in <see cref="HelperNames"/>.
/// </remarks>
/// <param name="Name">The helper's runtime name, matching upstream's <c>helperNameMap</c> value.</param>
public sealed record RuntimeHelper(string Name);
