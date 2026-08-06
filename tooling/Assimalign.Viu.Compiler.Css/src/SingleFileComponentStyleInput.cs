namespace Assimalign.Viu.Compiler.Css;

/// <summary>
/// One <c>.viu</c> or compatible <c>.vue</c> component fed to
/// <see cref="SingleFileComponentStyleBundler.Bundle"/>: its
/// <see cref="FilePath"/> (used both to resolve the <c>data-v-&lt;hash&gt;</c> scope id and to order the
/// bundle deterministically) and its already-read <see cref="Text"/>. The reader — the <c>ViuBundleCss</c>
/// MSBuild task — performs the file I/O and hands the content in, so the bundler (and the whole Tooling core)
/// stays I/O-free (RS1035-clean by construction, reusable inside the analyzer sandbox too).
/// </summary>
/// <param name="FilePath">The component path (the same path the generator's <c>AdditionalText</c> carries, so the scope ids agree).</param>
/// <param name="Text">The full single-file-component text.</param>
public sealed record SingleFileComponentStyleInput(string FilePath, string Text);
