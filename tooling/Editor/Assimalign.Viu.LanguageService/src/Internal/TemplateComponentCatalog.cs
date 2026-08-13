using System;
using System.Collections.Generic;
using System.Linq;

using Assimalign.Viu.Compiler.SingleFileComponent;
using Assimalign.Viu.Syntax.Templates;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// The editor view of the compiler's component declaration catalog. Native/component precedence is
/// applied before the compiler name ladder, matching [SFC-CG-8]: PascalCase wins a collision, while a
/// lowercase native spelling remains native.
/// </summary>
internal sealed class TemplateComponentCatalog
{
    private static readonly HashSet<string> RuntimeSelectedTags = new(StringComparer.Ordinal)
    {
        "component",
        "Component",
    };

    private readonly IReadOnlyList<TemplateComponentDeclaration> declarations;

    internal TemplateComponentCatalog(
        IReadOnlyList<TemplateComponentDeclaration>? declarations)
    {
        this.declarations = declarations ?? Array.Empty<TemplateComponentDeclaration>();
        CompilerCatalog = this.declarations.Count == 0
            ? ComponentDeclarationCatalog.Empty
            : new ComponentDeclarationCatalog(
                new EquatableArray<ComponentDeclarationEntry>(
                    this.declarations
                        .Select(declaration => declaration.CompilerDeclaration)
                        .ToArray()));
    }

    /// <summary>The exact catalog consumed by <see cref="ComponentUsageValidator"/>.</summary>
    internal ComponentDeclarationCatalog CompilerCatalog { get; }

    /// <summary>
    /// Resolves an authored static component tag after applying native and runtime-selected tag
    /// precedence. Missing and ambiguous compiler-catalog entries both return false.
    /// </summary>
    internal bool TryResolve(
        string authoredTag,
        out TemplateComponentDeclaration declaration)
    {
        declaration = null!;
        if (string.IsNullOrEmpty(authoredTag) ||
            RuntimeSelectedTags.Contains(authoredTag) ||
            (!char.IsUpper(authoredTag[0]) && CompilerDomKnowledge.IsNativeTag(authoredTag)) ||
            !CompilerCatalog.TryResolve(authoredTag, out var resolved))
        {
            return false;
        }

        foreach (var candidate in declarations)
        {
            if (candidate.CompilerDeclaration.Equals(resolved))
            {
                declaration = candidate;
                return true;
            }
        }

        return false;
    }
}
