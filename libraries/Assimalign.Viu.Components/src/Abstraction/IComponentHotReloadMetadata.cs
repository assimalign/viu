using System;

namespace Assimalign.Viu.Components;

/// <summary>
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
public interface IComponentHotReloadMetadata
{
    /// <summary>Gets the stable compiler-generated identifier for the component source file.</summary>
    string ComponentIdentifier { get; }

    /// <summary>Gets the compiler-generated type whose method changes with the template block.</summary>
    Type TemplateUpdateMarkerType { get; }

    /// <summary>Gets the compiler-generated type whose method changes with the C# script blocks.</summary>
    Type ScriptUpdateMarkerType { get; }

    /// <summary>Gets the compiler-generated type whose method changes with the style blocks.</summary>
    Type StyleUpdateMarkerType { get; }
}
