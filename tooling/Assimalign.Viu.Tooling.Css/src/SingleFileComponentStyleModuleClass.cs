namespace Assimalign.Viu.Tooling.Css;

/// <summary>
/// One entry of a component's CSS Modules map ([V01.01.06.06]) produced by
/// <see cref="SingleFileComponentStyleCompiler"/>: the <see cref="Accessor"/> the class belongs to (the
/// C# name for a <c>$style</c> object), the <see cref="Original"/> class name as authored, and the
/// <see cref="Hashed"/> name it compiled to. The generator host maps these into the typed,
/// source-generated <c>$style</c> accessor, so a template refers to a module class through a compiled
/// member and a typo is a build error rather than a silently missing class. The <c>ViuBundleCss</c> task host ignores
/// this map (it bundles the already-hashed <see cref="SingleFileComponentStyleCompilation.ExtractedStyles"/>
/// text) — the entries exist so the two hosts share one compilation without the task re-deriving names.
/// </summary>
/// <param name="Accessor">The generated accessor class name (default module → <c>Style</c>, <c>module="foo"</c> → <c>Foo</c>).</param>
/// <param name="Original">The original class name exactly as authored (without the leading <c>.</c>).</param>
/// <param name="Hashed">The locally-hashed class name the selector compiled to.</param>
/// <param name="Module">The authored module name (<c>module="name"</c>), or <see langword="null"/> for the default module.</param>
public sealed record SingleFileComponentStyleModuleClass(string Accessor, string Original, string Hashed, string? Module);
