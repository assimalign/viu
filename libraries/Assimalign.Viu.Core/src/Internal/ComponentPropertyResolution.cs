using System;
using System.Collections.Generic;

namespace Assimalign.Viu;

/// <summary>
/// Splits a component vnode's props into declared props and fallthrough attrs and applies them
/// to the instance — the C# port of <c>initProps</c>/<c>updateProps</c> in
/// <c>packages/runtime-core/src/componentProps.ts</c>
/// (https://vuejs.org/guide/components/props.html): camelCase/kebab-case equivalence, defaults
/// (factory defaults per instance), required/validator dev warnings naming the component and
/// prop, declared-emit handler exclusion from attrs, and change-only application so the child
/// re-renders only when a value it reads actually changed.
/// </summary>
internal static class ComponentPropertyResolution
{
    internal static void Resolve(ComponentInstance instance, VirtualNode virtualNode)
    {
        var declared = instance.DeclaredProperties;
        HashSet<string>? providedNames = null;
        List<KeyValuePair<string, object?>>? attributes = null;
        if (virtualNode.Properties is not null)
        {
            foreach (var (name, value) in virtualNode.Properties)
            {
                if (IsReservedProperty(name))
                {
                    continue;
                }
                if (declared is not null && declared.TryGetValue(name, out var declaration))
                {
                    (providedNames ??= new HashSet<string>(StringComparer.Ordinal)).Add(declaration.Name);
                    if (declaration.Validator is not null && !declaration.Validator(value))
                    {
                        RuntimeWarnings.Warn(
                            $"Invalid prop: custom validator check failed for prop \"{declaration.Name}\" on "
                            + $"component <{instance.DisplayName}>.");
                    }
                    instance.Properties.SetFromOwner(declaration.Name, value);
                }
                else if (!instance.IsDeclaredEmitHandlerName(name))
                {
                    (attributes ??= []).Add(new KeyValuePair<string, object?>(name, value));
                }
            }
        }
        if (declared is not null)
        {
            foreach (var declaration in declared.Values)
            {
                if (providedNames?.Contains(declaration.Name) == true)
                {
                    continue;
                }
                var wasProvided = instance.LastProvidedNames?.Contains(declaration.Name) == true;
                var hasValue = instance.Properties.Snapshot.ContainsKey(declaration.Name);
                if (declaration.Required)
                {
                    RuntimeWarnings.Warn(
                        $"Missing required prop: \"{declaration.Name}\" on component <{instance.DisplayName}>.");
                }
                if (declaration.DefaultFactory is not null || declaration.DefaultValue is not null)
                {
                    // Apply the default on first resolve or when the parent just withdrew the
                    // prop — never re-run a factory while its default is already in place
                    // (a fresh instance per pass would spuriously re-render the child).
                    if (!hasValue || wasProvided)
                    {
                        instance.Properties.SetFromOwner(declaration.Name, declaration.ResolveDefault());
                    }
                }
                else if (wasProvided)
                {
                    instance.Properties.RemoveFromOwner(declaration.Name);
                }
            }
        }
        instance.LastProvidedNames = providedNames;
        instance.Attributes.ReplaceFrom(attributes);
    }

    private static bool IsReservedProperty(string name)
        => string.Equals(name, "key", StringComparison.Ordinal)
            || string.Equals(name, "ref", StringComparison.Ordinal)
            || name.StartsWith("onVnode", StringComparison.Ordinal);
}
