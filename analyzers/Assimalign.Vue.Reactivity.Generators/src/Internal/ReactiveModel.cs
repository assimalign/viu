using System;

namespace Assimalign.Vue.Reactivity.Generators;

/// <summary>
/// The value-equatable description of one <c>[Reactive]</c>/<c>[ShallowReactive]</c> class the
/// generator emits an implementation for. Deliberately free of syntax nodes and symbols so the
/// incremental pipeline caches on it: an unrelated edit that produces an equal model re-emits nothing.
/// </summary>
internal readonly record struct ReactiveClassModel(
    string? Namespace,
    EquatableArray<ContainingTypeInfo> ContainingTypes,
    string TypeName,
    string TypeParameterList,
    string AccessibilityKeyword,
    bool Shallow,
    bool Readonly,
    EquatableArray<ReactivePropertyModel> Properties)
{
    /// <summary>A stable, unique file hint for <c>AddSource</c> built from the fully-qualified name.</summary>
    public string HintName
    {
        get
        {
            var prefix = string.IsNullOrEmpty(Namespace) ? string.Empty : Namespace + ".";
            var containers = string.Empty;
            foreach (var container in ContainingTypes)
            {
                containers += container.Name + ".";
            }
            return prefix + containers + TypeName + ".Reactive.g.cs";
        }
    }
}

/// <summary>One reactive partial property to implement: its name, type, generated field names, and accessors.</summary>
internal readonly record struct ReactivePropertyModel(
    string Name,
    string TypeFullName,
    string ValueFieldName,
    string DependencyFieldName,
    string AccessibilityKeyword,
    string SetterModifier,
    bool IsValueType);

/// <summary>An enclosing type that must be re-declared <c>partial</c> around the generated member.</summary>
internal readonly record struct ContainingTypeInfo(string Keyword, string Name, string TypeParameterList);

/// <summary>
/// The transform result for one attributed class: the model to emit (when valid) and any diagnostics
/// to report. Both are value-equatable so the pipeline stays cacheable.
/// </summary>
internal readonly record struct ReactiveGeneratorResult(ReactiveClassModel? Model, EquatableArray<DiagnosticInfo> Diagnostics);
