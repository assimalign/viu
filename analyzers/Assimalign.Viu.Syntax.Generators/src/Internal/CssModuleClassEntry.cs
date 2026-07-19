namespace Assimalign.Viu.Syntax.Generators;

/// <summary>
/// One entry of a component's CSS Modules map ([V01.01.06.06]): the <see cref="Accessor"/> the class
/// belongs to (the C# analogue of a <c>$style</c> object name), the <see cref="Original"/> class name as
/// authored, and the <see cref="Hashed"/> name it compiled to. The emitter groups entries by
/// <see cref="Accessor"/> into a nested static class whose <c>const</c> members mirror the declared class
/// names, so a reference to a missing class is a compile error — the typed, source-generated equivalent of
/// Vue's <c>$style</c> / <c>useCssModule()</c> (https://vuejs.org/api/sfc-css-features.html#css-modules).
/// A <see langword="readonly"/> <see langword="record"/> <see langword="struct"/> so it is value-equatable
/// and rides inside the cached model without defeating the incremental cache.
/// </summary>
/// <param name="Accessor">The generated accessor class name (default module → <c>Style</c>, <c>module="foo"</c> → <c>Foo</c>).</param>
/// <param name="Original">The original class name exactly as authored (without the leading <c>.</c>).</param>
/// <param name="Hashed">The locally-hashed class name the selector compiled to.</param>
internal readonly record struct CssModuleClassEntry(string Accessor, string Original, string Hashed);
