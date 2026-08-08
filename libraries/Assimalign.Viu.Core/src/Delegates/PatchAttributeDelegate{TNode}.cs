using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// Applies one immutable element-binding difference to a host element.
/// </summary>
/// <typeparam name="TNode">The opaque host-node type.</typeparam>
/// <param name="element">The host element receiving the difference.</param>
/// <param name="previousBinding">The prior binding, or null while mounting.</param>
/// <param name="nextBinding">The next binding, or null while removing.</param>
/// <remarks>Specified by <c>[RND-HOST-1]</c> and <c>[RND-PATCH-5]</c>.</remarks>
public delegate void PatchAttributeDelegate<TNode>(
    TNode element,
    ElementBinding? previousBinding,
    ElementBinding? nextBinding)
    where TNode : notnull;
