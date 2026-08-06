using System;
using System.ComponentModel;

namespace Assimalign.Viu.Components;

/// <summary>
/// Compiler-generated code only; not part of the supported Viu API.
/// Supplies compiler-generated change markers for a component that participates in
/// development-time Hot Reload.
/// </summary>
/// <remarks>
/// The marker types let Core classify template, script, and style deltas from the runtime-provided
/// updated-type set without reading patched method bodies through reflection. On .NET 10 browser
/// WebAssembly, managed template and script changes remount the affected component because already
/// transformed call sites can continue targeting the previous method body. Style changes remain
/// mounted and are handled by the Viu CSS Hot Reload worker.
/// </remarks>
[System.ComponentModel.EditorBrowsable(EditorBrowsableState.Never)]
public interface IComponentHotReloadMetadata
{
    /// <summary>
    /// Compiler-generated code only; not part of the supported Viu API.
    /// Gets the stable compiler-generated identifier for the component source file.
    /// </summary>
    [System.ComponentModel.EditorBrowsable(EditorBrowsableState.Never)]
    string ComponentIdentifier { get; }

    /// <summary>
    /// Compiler-generated code only; not part of the supported Viu API.
    /// Gets the compiler-generated type whose method changes with the template block.
    /// </summary>
    [System.ComponentModel.EditorBrowsable(EditorBrowsableState.Never)]
    Type TemplateUpdateMarkerType { get; }

    /// <summary>
    /// Compiler-generated code only; not part of the supported Viu API.
    /// Gets the compiler-generated type whose method changes with the C# script blocks.
    /// </summary>
    [System.ComponentModel.EditorBrowsable(EditorBrowsableState.Never)]
    Type ScriptUpdateMarkerType { get; }

    /// <summary>
    /// Compiler-generated code only; not part of the supported Viu API.
    /// Gets the compiler-generated type whose method changes with the style blocks.
    /// </summary>
    [System.ComponentModel.EditorBrowsable(EditorBrowsableState.Never)]
    Type StyleUpdateMarkerType { get; }
}
