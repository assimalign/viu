using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// The contract-resolved view of one component invocation: declared parameters with defaults
/// applied, effective slots, and undeclared bindings that fall through to the rendered root.
/// Deliberately shares no interface with <see cref="ComponentInvocation"/> — the raw request and
/// the mounted view are different lifetimes with different names.
/// </summary>
public sealed class ComponentBindings
{
    /// <summary>Initializes a resolved bindings view.</summary>
    /// <param name="parameters">Declared parameter values after defaults.</param>
    /// <param name="slots">Effective named slots.</param>
    /// <param name="fallthroughBindings">Undeclared bindings that fall through to the root.</param>
    public ComponentBindings(
        IReadOnlyDictionary<string, object?>? parameters = null,
        IReadOnlyDictionary<string, ComponentSlot>? slots = null,
        IReadOnlyDictionary<string, object?>? fallthroughBindings = null)
    {
        Parameters = CollectionSnapshot.CopyDictionary(parameters);
        Slots = CollectionSnapshot.CopyDictionary(slots);
        FallthroughBindings = CollectionSnapshot.CopyDictionary(fallthroughBindings);
    }

    /// <summary>Gets declared parameter values after alias matching and defaults.</summary>
    public IReadOnlyDictionary<string, object?> Parameters { get; }

    /// <summary>Gets the effective named slots.</summary>
    public IReadOnlyDictionary<string, ComponentSlot> Slots { get; }

    /// <summary>Gets undeclared bindings that fall through to the rendered root.</summary>
    public IReadOnlyDictionary<string, object?> FallthroughBindings { get; }

    /// <summary>
    /// Resolves a raw invocation against a contract: alias matching (exact, camelized,
    /// hyphenated), declared-versus-fallthrough splitting, and declared-listener consumption.
    /// Pure — per-mount concerns (default-value caching, initial-mount warning gating) belong to
    /// the runtime, which reports them from the returned diagnostics.
    /// </summary>
    /// <param name="contract">The static declaration.</param>
    /// <param name="invocation">The raw parent-supplied request.</param>
    /// <param name="diagnostics">An optional collector for resolution diagnostics.</param>
    /// <returns>The resolved view.</returns>
    /// <remarks>Illustrative resolution only in the contract model.</remarks>
    public static ComponentBindings Resolve(
        ComponentContract contract,
        ComponentInvocation invocation,
        ICollection<ComponentBindingDiagnostic>? diagnostics = null)
    {
        Dictionary<string, object?> parameters = [];
        Dictionary<string, object?> fallthrough = [];
        HashSet<string> declared = [];
        foreach (ComponentParameter parameter in contract.Parameters)
        {
            declared.Add(parameter.Name);
        }
        foreach (KeyValuePair<string, object?> argument in invocation.Arguments)
        {
            if (declared.Contains(NameNormalization.Camelize(argument.Key)))
            {
                parameters[NameNormalization.Camelize(argument.Key)] = argument.Value;
            }
            else
            {
                fallthrough[argument.Key] = argument.Value;
            }
        }
        foreach (ComponentParameter parameter in contract.Parameters)
        {
            if (parameter.IsRequired && !parameters.ContainsKey(parameter.Name))
            {
                diagnostics?.Add(new ComponentBindingDiagnostic(
                    ComponentBindingDiagnosticKind.MissingRequiredParameter,
                    parameter.Name,
                    $"Required parameter '{parameter.Name}' was not supplied."));
            }
        }
        return new ComponentBindings(parameters, invocation.Slots, fallthrough);
    }
}
