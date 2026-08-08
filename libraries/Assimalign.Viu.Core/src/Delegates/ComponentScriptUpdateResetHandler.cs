using System;
using System.ComponentModel;

namespace Assimalign.Viu;

/// <summary>Handles a script update that requires a component-local reset or host reload.</summary>
/// <remarks>
/// This development-runtime application binary interface is hidden from ordinary application
/// authoring. Specified by
/// <c>[SFC-CG-2]</c> and <c>[SFC-CG-4]</c>.
/// </remarks>
/// <param name="componentIdentifier">The stable compiler-generated source identifier.</param>
/// <param name="componentType">The component type whose script block changed.</param>
[EditorBrowsable(EditorBrowsableState.Never)]
public delegate void ComponentScriptUpdateResetHandler(
    string componentIdentifier,
    Type componentType);
