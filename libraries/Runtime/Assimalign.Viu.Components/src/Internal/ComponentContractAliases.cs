using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// The alias tables one contract's resolution needs: every declared parameter and event under its
/// exact, camelized, and hyphenated spelling, plus the duplicate-alias diagnostics those
/// declarations produce. A contract is immutable, so the tables are built once per contract and
/// shared by every resolution, instead of being rebuilt — with three name normalizations per
/// declaration — on every component update. The diagnostics are replayed into each resolution's
/// collector in declaration order, so a caller observes exactly what a fresh build would report.
/// Specified by <c>[CMP-12]</c>.
/// </summary>
internal sealed class ComponentContractAliases
{
    private readonly List<ComponentBindingDiagnostic>? _diagnostics;

    internal ComponentContractAliases(ComponentContract contract)
    {
        Parameters = new Dictionary<string, ComponentParameter>(
            contract.Parameters.Count * 3,
            StringComparer.Ordinal);
        Events = new Dictionary<string, ComponentEvent>(
            contract.Events.Count * 3,
            StringComparer.Ordinal);

        foreach (ComponentParameter parameter in contract.Parameters)
        {
            AddAliases(Parameters, parameter.Name, parameter, "parameter", ref _diagnostics);
        }

        foreach (ComponentEvent componentEvent in contract.Events)
        {
            AddAliases(Events, componentEvent.Name, componentEvent, "event", ref _diagnostics);
        }
    }

    /// <summary>Every declared parameter under each spelling an argument may use.</summary>
    internal Dictionary<string, ComponentParameter> Parameters { get; }

    /// <summary>Every declared event under each spelling a listener may use.</summary>
    internal Dictionary<string, ComponentEvent> Events { get; }

    /// <summary>Replays the declaration-time duplicate-alias diagnostics into <paramref name="diagnostics"/>.</summary>
    /// <param name="diagnostics">The resolution's collector, or null when the caller collects nothing.</param>
    internal void ReportDiagnostics(ICollection<ComponentBindingDiagnostic>? diagnostics)
    {
        if (diagnostics is null || _diagnostics is null)
        {
            return;
        }

        foreach (ComponentBindingDiagnostic diagnostic in _diagnostics)
        {
            diagnostics.Add(diagnostic);
        }
    }

    private static void AddAliases<TDeclaration>(
        Dictionary<string, TDeclaration> aliases,
        string name,
        TDeclaration declaration,
        string declarationKind,
        ref List<ComponentBindingDiagnostic>? diagnostics)
        where TDeclaration : class
    {
        AddAlias(aliases, name, declaration, declarationKind, ref diagnostics);

        string camelizedName = NameNormalization.Camelize(name);
        if (!string.Equals(camelizedName, name, StringComparison.Ordinal))
        {
            AddAlias(aliases, camelizedName, declaration, declarationKind, ref diagnostics);
        }

        string hyphenatedName = NameNormalization.Hyphenate(name);
        if (!string.Equals(hyphenatedName, name, StringComparison.Ordinal)
            && !string.Equals(hyphenatedName, camelizedName, StringComparison.Ordinal))
        {
            AddAlias(aliases, hyphenatedName, declaration, declarationKind, ref diagnostics);
        }
    }

    private static void AddAlias<TDeclaration>(
        Dictionary<string, TDeclaration> aliases,
        string alias,
        TDeclaration declaration,
        string declarationKind,
        ref List<ComponentBindingDiagnostic>? diagnostics)
        where TDeclaration : class
    {
        if (aliases.TryGetValue(alias, out TDeclaration? existing))
        {
            if (!ReferenceEquals(existing, declaration))
            {
                (diagnostics ??= []).Add(new ComponentBindingDiagnostic(
                    ComponentBindingDiagnosticKind.DuplicateAlias,
                    alias,
                    $"Component {declarationKind} alias '{alias}' is declared more than once; "
                        + "the first declaration is used."));
            }

            return;
        }

        aliases.Add(alias, declaration);
    }
}
