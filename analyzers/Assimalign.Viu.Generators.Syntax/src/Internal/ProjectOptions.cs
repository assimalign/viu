namespace Assimalign.Viu.Syntax.Generators;

/// <summary>
/// The consuming project's properties the generated names depend on, extracted from the analyzer config
/// as a value-equatable record so an unrelated build-property change does not invalidate the pipeline.
/// </summary>
/// <param name="RootNamespace">The consuming project's root namespace, or <see langword="null"/>.</param>
/// <param name="ProjectDirectory">The consuming project's directory, or <see langword="null"/>.</param>
internal readonly record struct ProjectOptions(string? RootNamespace, string? ProjectDirectory);
