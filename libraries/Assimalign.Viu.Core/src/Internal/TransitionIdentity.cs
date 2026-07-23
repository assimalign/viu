using Assimalign.Viu.Components;

namespace Assimalign.Viu;

internal readonly record struct TransitionIdentity(
    ComponentKind Kind,
    object Type,
    object? Key);
