using System.Collections.Generic;

namespace Assimalign.Viu.DevTools;

/// <summary>The dev tools value payload exchanged by the inspection protocol.</summary>
/// <remarks>Data-only contract; mutable collections are not thread-safe. Specified by <c>[DVT-2]</c>, <c>[DVT-13]</c>, and <c>[DVT-14]</c>.</remarks>
/// <param name="Kind">The event or value discriminator.</param>
/// <param name="TypeName">The diagnostic CLR type name.</param>
/// <param name="DisplayValue">The safely formatted scalar or placeholder reason.</param>
/// <param name="Expandable">Whether deeper content may be requested.</param>
/// <param name="Path">The ordered case-sensitive inspection path segments.</param>
/// <param name="Children">The children at the requested depth.</param>
public sealed record DevToolsValuePayload(
    string Kind,
    string TypeName,
    string? DisplayValue,
    bool Expandable,
    List<string> Path,
    List<DevToolsNamedValuePayload> Children);
