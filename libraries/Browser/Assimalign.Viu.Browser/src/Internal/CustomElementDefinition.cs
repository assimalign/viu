using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser;

internal sealed class CustomElementDefinition
{
    private CustomElementDefinition(
        ComponentReference component,
        ComponentContract contract,
        bool useShadowRoot,
        string[] parameterNames,
        string[] attributeNames,
        IReadOnlyDictionary<string, ComponentParameter> parameters)
    {
        Component = component;
        Contract = contract;
        UseShadowRoot = useShadowRoot;
        ParameterNames = parameterNames;
        AttributeNames = attributeNames;
        Parameters = parameters;
    }

    internal ComponentReference Component { get; }

    internal ComponentContract Contract { get; }

    internal bool UseShadowRoot { get; }

    internal string[] ParameterNames { get; }

    internal string[] AttributeNames { get; }

    internal IReadOnlyDictionary<string, ComponentParameter> Parameters { get; }

    internal static CustomElementDefinition Create(
        ComponentReference component,
        ComponentContract contract,
        CustomElementOptions? options)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(contract);

        bool useShadowRoot = options?.UseShadowRoot ?? true;
        Func<string, string>? attributeNameMapper = options?.AttributeNameMapper;
        string[] parameterNames = new string[contract.Parameters.Count];
        string[] attributeNames = new string[parameterNames.Length];
        Dictionary<string, ComponentParameter> parameters = new(StringComparer.Ordinal);
        HashSet<string> observedNames = new(StringComparer.Ordinal);

        for (int index = 0; index < parameterNames.Length; index++)
        {
            ComponentParameter parameter = contract.Parameters[index];
            if (!parameters.TryAdd(parameter.Name, parameter))
            {
                throw new ArgumentException(
                    $"Duplicate custom-element parameter '{parameter.Name}'.",
                    nameof(component));
            }

            parameterNames[index] = parameter.Name;
            if (!CustomElementValues.IsReflectable(parameter.ParameterType))
            {
                attributeNames[index] = string.Empty;
                continue;
            }

            string? attributeName = attributeNameMapper is null
                ? CustomElementValues.Hyphenate(parameter.Name)
                : attributeNameMapper(parameter.Name);
            if (attributeName is null)
            {
                throw new ArgumentException(
                    $"The custom-element attribute mapper returned null for parameter '{parameter.Name}'.",
                    nameof(options));
            }

            CustomElementValues.ValidateAttributeName(attributeName, observedNames, nameof(options));
            attributeNames[index] = attributeName;
        }

        return new CustomElementDefinition(
            component,
            contract,
            useShadowRoot,
            parameterNames,
            attributeNames,
            parameters);
    }
}
