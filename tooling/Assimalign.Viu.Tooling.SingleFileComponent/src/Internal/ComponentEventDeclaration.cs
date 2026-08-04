namespace Assimalign.Viu.Tooling.SingleFileComponent;

/// <summary>
/// One <c>[Event]</c>-declared component output read out of an <c>@script</c> block: the canonical
/// event name and everything needed to implement the attributed <c>partial void</c> method as a typed
/// emit. The generator emits both the <c>ComponentEvent</c> declaration and the method body, so the
/// component's own code never spells the event name as a string literal. Specified by <c>[CMP-30]</c>.
/// A <see langword="readonly"/> <see langword="record"/> <see langword="struct"/> so it rides inside the
/// incremental generator's cached model without defeating the cache.
/// </summary>
/// <param name="Name">The canonical event name, derived from <paramref name="MethodName"/> or set explicitly [CMP-27].</param>
/// <param name="MethodName">The declaring method's C# name.</param>
/// <param name="Modifiers">The method's declared modifiers, space-separated, reproduced verbatim on the generated implementing declaration.</param>
/// <param name="ParameterList">The method's parameter list source text, including the enclosing parentheses.</param>
/// <param name="ArgumentList">The comma-separated parameter names forwarded to <c>Emit</c>; empty for an argument-free event.</param>
/// <param name="ArgumentCount">The emitted argument count the synthesized validator asserts.</param>
internal readonly record struct ComponentEventDeclaration(
    string Name,
    string MethodName,
    string Modifiers,
    string ParameterList,
    string ArgumentList,
    int ArgumentCount);
