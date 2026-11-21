using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// The contract-resolved view of one component invocation: supplied declared parameters,
/// effective slots, and undeclared bindings that fall through to the rendered root.
/// Deliberately shares no interface with <see cref="ComponentInvocation"/> — the raw request and
/// the mounted view are different lifetimes with different names.
/// </summary>
/// <remarks>
/// Resolution is an ordinal, allocation-bounded data transformation. Default factories are not
/// evaluated here: the runtime owns their per-mounted-instance cache. The runtime also decides
/// when each returned diagnostic is reported, including initial-mount-only warning gates.
/// Specified by <c>[CMP-2]</c>, <c>[CMP-12]</c>, <c>[CMP-13]</c>, and <c>[CMP-17]</c>.
/// </remarks>
public sealed class ComponentBindings
{
    /// <summary>Initializes an immutable resolved-bindings snapshot.</summary>
    /// <param name="parameters">Supplied declared parameter values keyed by canonical name.</param>
    /// <param name="slots">Effective named slots.</param>
    /// <param name="fallthroughBindings">Undeclared bindings that fall through to the root.</param>
    public ComponentBindings(
        IReadOnlyDictionary<string, object?>? parameters = null,
        IReadOnlyDictionary<string, ComponentSlot>? slots = null,
        IReadOnlyDictionary<string, object?>? fallthroughBindings = null)
    {
        Parameters = CollectionSnapshot.CopyDictionary(parameters);
        Slots = CollectionSnapshot.CopyNonNullDictionary(slots, nameof(slots));
        FallthroughBindings = CollectionSnapshot.CopyDictionary(fallthroughBindings);
    }

    /// <summary>Gets supplied declared parameter values after alias matching.</summary>
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
    public static ComponentBindings Resolve(
        ComponentContract contract,
        ComponentInvocation invocation,
        ICollection<ComponentBindingDiagnostic>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(invocation);

        // The alias tables depend only on the immutable contract, so they are built once per
        // contract and replay their declaration-time diagnostics into every resolution.
        ComponentContractAliases aliases = contract.Aliases;
        aliases.ReportDiagnostics(diagnostics);
        Dictionary<string, ComponentParameter> parameterAliases = aliases.Parameters;
        Dictionary<string, ComponentEvent> eventAliases = aliases.Events;

        Dictionary<string, object?> parameters = new(invocation.Arguments.Count, StringComparer.Ordinal);
        Dictionary<string, string> suppliedAliases = new(StringComparer.Ordinal);
        Dictionary<string, object?> fallthrough = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> argument in invocation.Arguments)
        {
            if (IsComponentNodeLifecycleName(argument.Key))
            {
                continue;
            }

            if (parameterAliases.TryGetValue(argument.Key, out ComponentParameter? parameter))
            {
                if (suppliedAliases.TryGetValue(parameter.Name, out string? previousAlias))
                {
                    diagnostics?.Add(new ComponentBindingDiagnostic(
                        ComponentBindingDiagnosticKind.DuplicateAlias,
                        parameter.Name,
                        $"Component parameter '{parameter.Name}' was supplied through both "
                            + $"'{previousAlias}' and '{argument.Key}'; the later value is used."));
                }

                suppliedAliases[parameter.Name] = argument.Key;
                parameters[parameter.Name] = argument.Value;
                if (parameter.Validator is not null && !parameter.Validator(argument.Value))
                {
                    diagnostics?.Add(new ComponentBindingDiagnostic(
                        ComponentBindingDiagnosticKind.ParameterValidationFailed,
                        parameter.Name,
                        $"Invalid value was supplied for component parameter '{parameter.Name}'."));
                }
            }
            else if (!IsDeclaredEventListener(argument.Key, eventAliases))
            {
                fallthrough[argument.Key] = argument.Value;
            }
        }

        foreach (ComponentParameter parameter in contract.Parameters)
        {
            if (parameter.IsRequired
                && parameter.DefaultFactory is null
                && !parameters.ContainsKey(parameter.Name))
            {
                diagnostics?.Add(new ComponentBindingDiagnostic(
                    ComponentBindingDiagnosticKind.MissingRequiredParameter,
                    parameter.Name,
                    $"Required parameter '{parameter.Name}' was not supplied."));
            }
        }
        // The tables were built here and nothing else holds them, so the bindings adopt them as
        // snapshots instead of copying them a second time; the invocation's slot snapshot is
        // adopted the same way.
        return new ComponentBindings(
            new SnapshotDictionary<object?>(parameters, isNullFree: false),
            invocation.Slots,
            new SnapshotDictionary<object?>(fallthrough, isNullFree: false));
    }

    private static bool IsDeclaredEventListener(
        string argumentName,
        Dictionary<string, ComponentEvent> eventAliases)
    {
        if (argumentName.Length <= 2
            || argumentName[0] != 'o'
            || argumentName[1] != 'n'
            || !char.IsAsciiLetterUpper(argumentName[2]))
        {
            return false;
        }

        string eventName = char.ToLowerInvariant(argumentName[2]) + argumentName[3..];
        if (eventAliases.ContainsKey(eventName))
        {
            return true;
        }

        return eventName.EndsWith("Once", StringComparison.Ordinal)
            && eventAliases.ContainsKey(eventName[..^4]);
    }

    private static bool IsComponentNodeLifecycleName(string name) =>
        name.StartsWith("onVnode", StringComparison.Ordinal);
}
