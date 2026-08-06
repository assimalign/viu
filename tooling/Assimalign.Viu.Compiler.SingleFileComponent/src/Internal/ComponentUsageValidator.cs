using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>
/// Checks a template's component usages against the declarations the compilation can read
/// ([SFC-USE-2]). It is the payoff of the attribute declaration form: because a <c>[Parameter]</c>
/// survives into metadata, a component's input surface is readable from a referenced assembly, so
/// <c>&lt;FeatureCard unknown="y"&gt;</c> can be reported where the template is compiled rather than
/// discovered as a silently ignored argument at run time.
/// <para>
/// The governing rule is that a <b>false positive is worse than a false negative</b>. Every input that
/// cannot be decided statically — a component the catalog does not resolve, an argument-spreading
/// <c>v-bind</c>, a non-literal bound expression, a hyphenated attribute name — makes the checker stay
/// silent rather than guess ([SFC-USE-5]).
/// </para>
/// </summary>
internal static class ComponentUsageValidator
{
    /// <summary>
    /// Validates <paramref name="usages"/> against <paramref name="catalog"/>, appending located
    /// diagnostics to <paramref name="diagnostics"/>.
    /// </summary>
    /// <param name="usages">The component usages collected from one template.</param>
    /// <param name="catalog">The declarations this compilation can read.</param>
    /// <param name="diagnostics">The diagnostic accumulator.</param>
    public static void Validate(
        EquatableArray<ComponentUsage> usages,
        ComponentDeclarationCatalog catalog,
        List<DiagnosticInfo> diagnostics)
    {
        if (usages.Count == 0 || catalog.IsEmpty)
        {
            return;
        }

        foreach (var usage in usages)
        {
            if (usage.SuppliesUnknownArguments || !catalog.TryResolve(usage.Tag, out var entry))
            {
                continue;
            }

            ValidateUsage(usage, entry, diagnostics);
        }
    }

    private static void ValidateUsage(
        in ComponentUsage usage,
        in ComponentDeclarationEntry entry,
        List<DiagnosticInfo> diagnostics)
    {
        var supplied = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in usage.Properties)
        {
            if (property.Kind is ComponentUsagePropertyKind.Listener
                or ComponentUsagePropertyKind.Directive)
            {
                continue;
            }

            if (!TryFindParameter(entry, property.Name, out var parameter))
            {
                if (!FallthroughAttributeKnowledge.IsFallthroughCandidate(property.AuthoredName))
                {
                    diagnostics.Add(new DiagnosticInfo(
                        SingleFileComponentDiagnostics.UnknownComponentParameter,
                        property.Location,
                        $"Component '{usage.Tag}' declares no parameter named "
                        + $"'{property.AuthoredName}'. {DescribeDeclared(entry)} An undeclared "
                        + "attribute still falls through to the component's rendered root, so this is a "
                        + "warning, not an error."));
                }

                continue;
            }

            supplied.Add(parameter.Name);
            ValidateType(usage.Tag, property, parameter, diagnostics);
        }

        foreach (var parameter in entry.Parameters)
        {
            if (parameter.IsRequired && !supplied.Contains(parameter.Name))
            {
                diagnostics.Add(new DiagnosticInfo(
                    SingleFileComponentDiagnostics.MissingRequiredComponentParameter,
                    usage.Location,
                    $"Component '{usage.Tag}' requires the parameter '{parameter.Name}', which this "
                    + "usage does not supply."));
            }
        }
    }

    private static void ValidateType(
        string tag,
        in ComponentUsageProperty property,
        in ComponentParameterDeclaration parameter,
        List<DiagnosticInfo> diagnostics)
    {
        // Only the two decidable directions are reported. Everything else — an unknown declared type, an
        // unknown supplied type, `object`, a reference type — is left alone, because deciding it needs a
        // semantic model the projection deliberately does not have.
        if (parameter.TypeKind == ComponentValueKind.Value &&
            property.ValueKind == ComponentValueKind.Text &&
            property.Kind == ComponentUsagePropertyKind.Static)
        {
            diagnostics.Add(new DiagnosticInfo(
                SingleFileComponentDiagnostics.IncompatibleComponentArgument,
                property.Location,
                $"Component '{tag}' declares parameter '{parameter.Name}' as '{parameter.TypeText}', "
                + "but a plain attribute always supplies a string. Bind the value instead: "
                + $"':{property.AuthoredName}=\"…\"'."));
            return;
        }

        if (parameter.TypeKind == ComponentValueKind.Text &&
            property.ValueKind == ComponentValueKind.Value)
        {
            diagnostics.Add(new DiagnosticInfo(
                SingleFileComponentDiagnostics.IncompatibleComponentArgument,
                property.Location,
                $"Component '{tag}' declares parameter '{parameter.Name}' as "
                + $"'{parameter.TypeText}', but this usage binds a non-string literal."));
        }
    }

    private static bool TryFindParameter(
        in ComponentDeclarationEntry entry,
        string canonicalName,
        out ComponentParameterDeclaration parameter)
    {
        foreach (var declared in entry.Parameters)
        {
            if (string.Equals(
                    ComponentDeclarationNames.Canonicalize(declared.Name),
                    canonicalName,
                    StringComparison.Ordinal))
            {
                parameter = declared;
                return true;
            }
        }

        parameter = default;
        return false;
    }

    private static string DescribeDeclared(in ComponentDeclarationEntry entry)
    {
        if (entry.Parameters.Count == 0)
        {
            return "It declares no parameters.";
        }

        var names = new string[entry.Parameters.Count];
        for (var index = 0; index < entry.Parameters.Count; index++)
        {
            names[index] = "'" + entry.Parameters[index].Name + "'";
        }

        return "It declares " + string.Join(", ", names) + ".";
    }
}
