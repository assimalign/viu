using Assimalign.Viu.Syntax.Templates;

namespace Assimalign.Viu.Syntax.Generators;

/// <summary>
/// One classified <c>@script</c> member: its declared name and the <see cref="BindingType"/> the
/// render-code-generation path ([V01.01.05.04]/[V01.01.05.05]) reads to decide where a
/// <c>Reference&lt;T&gt;.Value</c> unwrap belongs. The C# analogue of one entry in Vue 3.5's
/// <c>BindingMetadata</c> map (<c>@vue/compiler-sfc</c> <c>compileScript()</c>, projected from
/// <c>BindingTypes</c>). A <see langword="readonly"/> <see langword="record"/> <see langword="struct"/> so
/// it is value-equatable and rides inside the incremental generator's cached model without defeating the
/// cache — the contract the <see cref="EquatableArray{T}"/> it is stored in requires.
/// </summary>
/// <param name="Name">The member name exactly as declared in the <c>@script</c> block.</param>
/// <param name="Type">The classified binding type that drives ref-unwrapping decisions in the template.</param>
internal readonly record struct ScriptBinding(string Name, BindingType Type);
