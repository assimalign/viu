namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>
/// The development-only hot-reload identity and independent block-category content hashes emitted for
/// one single-file component. The values are plain strings so the incremental pipeline remains fully
/// value-equatable and the Release pipeline can omit the contract entirely.
/// </summary>
/// <param name="ComponentIdentifier">
/// The stable component identifier derived from the normalized project-relative source path.
/// </param>
/// <param name="TemplateContentHash">
/// The deterministic hash of the template block, including its options and raw content.
/// </param>
/// <param name="ScriptContentHash">
/// The deterministic hash of the ordinary and setup script blocks in source order, including their
/// options and raw content.
/// </param>
/// <param name="StyleContentHash">
/// The deterministic hash of every style block in source order, including each block's options and raw
/// content.
/// </param>
internal readonly record struct SingleFileComponentHotReloadMetadata(
    string ComponentIdentifier,
    string TemplateContentHash,
    string ScriptContentHash,
    string StyleContentHash);
