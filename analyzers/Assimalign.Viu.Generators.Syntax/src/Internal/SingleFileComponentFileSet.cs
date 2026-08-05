using System;

using Assimalign.Viu.Tooling.Css;
using Assimalign.Viu.Tooling.SingleFileComponent;

namespace Assimalign.Viu.Generators.Syntax;

/// <summary>
/// The two cross-file facts a single component's projection needs but cannot see from its own path:
/// which canonical <c>.viu</c> base paths exist (so a same-base <c>.vue</c> peer knows it is shadowed,
/// [VUE-7]) and which components must carry the hint-name case discriminator ([SFC-CG-5],
/// [V01.01.06.10.01]). Both are value-equatable arrays, so this record is a legal incremental-pipeline
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
internal readonly record struct SingleFileComponentFileSet(
    EquatableArray<string> CanonicalBasePaths,
    EquatableArray<string> CaseDiscriminatedPaths)
{
    /// <summary>The set for a compilation with no component files.</summary>
    public static readonly SingleFileComponentFileSet Empty =
        new(EquatableArray<string>.Empty, EquatableArray<string>.Empty);

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
    {
        foreach (var discriminatedPath in CaseDiscriminatedPaths)
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
