namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// A by-name reference to a runtime helper the generated render function will import from the runtime
/// (e.g. <c>createElementBlock</c>, <c>renderList</c>, <c>withModifiers</c>).
/// </summary>
/// <remarks>
/// The compiler NEVER references the runtime assembly: helpers are carried as plain names and resolved to
/// concrete members by code generation ([V01.01.05.05]) and the runtime ([V01.01.05.02]) — the one-way
/// name-binding contract of <c>[SFC-CG-2]</c>. Two helpers with
/// the same <see cref="Name"/> are equal, so a transform may request a helper without coordinating with
/// any other transform and the emitter still imports it once; the canonical names
/// live in <see cref="HelperNames"/>.
/// </remarks>
/// <param name="Name">The helper's runtime member name, used verbatim in generated code.</param>
public sealed record RuntimeHelper(string Name);
