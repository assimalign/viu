using System;

namespace Assimalign.Viu;

/// <summary>
/// Handles a Hot Reload script update that requires the owning host to reset or remount the
/// component.
/// </summary>
/// <param name="componentIdentifier">
/// The stable compiler-generated identifier for the component source file.
/// </param>
/// <param name="componentType">The generated component type whose script changed.</param>
public delegate void ComponentScriptUpdateResetHandler(
    string componentIdentifier,
    Type componentType);
