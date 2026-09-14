using System;

using Assimalign.Viu.Compiler.Css;
using Assimalign.Viu.Compiler.SingleFileComponent;

namespace Assimalign.Viu.Generators.Syntax;

/// <summary>
/// The cross-file facts a single component's projection needs but cannot see from its own path:
/// which canonical <c>.viu</c> base paths exist (so a same-base <c>.vue</c> peer knows it is shadowed,
/// [VUE-7]) and which components must carry the hint-name case discriminator ([SFC-CG-5],
/// [V01.01.06.10.01]), plus namespace disambiguation and residual identity collisions [SFC-CG-10].
/// These are value-equatable arrays, so this record is a legal incremental-pipeline
/// value: it changes only when component files are added, removed, or renamed — never when one is
/// edited — and every per-file step stays cached across an ordinary edit.
/// </summary>
/// <param name="CanonicalBasePaths">
/// Every <c>.viu</c> file's path with its extension removed, ordinally sorted.
/// </param>
/// <param name="CaseDiscriminatedPaths">
/// The exact paths of the components whose readable hint names collide with another component's by case
/// alone, ordinally sorted. Empty in every compilation with no such collision, which is the norm.
/// </param>
/// <param name="NamespaceDiscriminatedPaths">Components moved into the generated leaf namespace, ordinally sorted.</param>
/// <param name="IdentityCollidingPaths">Components that still have conflicting generated identities, ordinally sorted.</param>
internal readonly record struct SingleFileComponentFileSet(
    EquatableArray<string> CanonicalBasePaths,
    EquatableArray<string> CaseDiscriminatedPaths,
    EquatableArray<string> NamespaceDiscriminatedPaths,
    EquatableArray<string> IdentityCollidingPaths)
{
    /// <summary>The set for a compilation with no component files.</summary>
    public static readonly SingleFileComponentFileSet Empty =
        new(EquatableArray<string>.Empty, EquatableArray<string>.Empty,
            EquatableArray<string>.Empty, EquatableArray<string>.Empty);

    /// <summary>Whether the generated class needs the leaf namespace specified by [SFC-CG-10].</summary>
    public bool RequiresNamespaceDiscriminator(string filePath) => ContainsPath(NamespaceDiscriminatedPaths, filePath);

    /// <summary>Whether [SFC-CG-10] requires a located error and omission of this component.</summary>
    public bool HasIdentityCollision(string filePath) => ContainsPath(IdentityCollidingPaths, filePath);

    /// <summary>
    /// Whether a canonical <c>.viu</c> component shadows the compatibility <c>.vue</c> file at
    /// <paramref name="componentBasePath"/>. Path identity follows the host operating system [VUE-7].
    /// </summary>
    /// <param name="componentBasePath">The extension-stripped, forward-slash-normalized component path.</param>
    /// <returns><see langword="true"/> when a same-directory, same-base <c>.viu</c> file exists.</returns>
    public bool ContainsCanonicalBasePath(string componentBasePath)
    {
        foreach (var canonicalBasePath in CanonicalBasePaths)
        {
            if (string.Equals(
                    componentBasePath,
                    canonicalBasePath,
                    SingleFileComponentPathComparison.Comparison))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether <paramref name="filePath"/> must take the hint-name case discriminator because another
    /// emitted component's readable hint name differs from its own only by case ([SFC-CG-5]).
    /// </summary>
    /// <param name="filePath">The component's path exactly as MSBuild supplied it.</param>
    /// <returns><see langword="true"/> when the discriminator is required.</returns>
    public bool RequiresCaseDiscriminator(string filePath)
        => ContainsPath(CaseDiscriminatedPaths, filePath);

    private static bool ContainsPath(EquatableArray<string> paths, string filePath)
    {
        foreach (var discriminatedPath in paths)
        {
            // Ordinal: the entries are the very paths the additional texts carry, and a case-differing
            // path is a DIFFERENT component here - the collision is exactly what is being resolved.
            if (string.Equals(filePath, discriminatedPath, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
