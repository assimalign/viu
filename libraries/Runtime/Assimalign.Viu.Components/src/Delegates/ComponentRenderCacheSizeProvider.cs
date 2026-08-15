namespace Assimalign.Viu.Components;

/// <summary>
/// Reports the exact render-cache slot count required by the currently installed compiled render
/// body.
/// </summary>
/// <remarks>
/// A generated <see cref="ComponentContract"/> retains this provider for its lifetime, and each
/// mount invokes it once while constructing the mount-owned <see cref="ComponentRenderFrame"/>.
/// Keeping the count behind a method body lets an accepted metadata update change template structure
/// without changing or reinitializing the generated contract field. Specified by
/// <c>[SFC-CG-9]</c> and <c>[SFC-OPT-1]</c> ([V01.01.06.14]).
/// </remarks>
/// <returns>The exact non-negative number of cache slots required by the current render body.</returns>
public delegate int ComponentRenderCacheSizeProvider();
