using System.Collections.Generic;

namespace Assimalign.Viu.DevTools;

/// <summary>The component tree snapshot payload exchanged by the inspection protocol.</summary>
/// <remarks>Data-only contract; mutable collections are not thread-safe. Specified by <c>[DVT-2]</c>, <c>[DVT-13]</c>, and <c>[DVT-14]</c>.</remarks>
/// <param name="Roots">The root nodes in presentation order.</param>
public sealed record ComponentTreeSnapshotPayload(
    List<ComponentTreeNodePayload> Roots);
