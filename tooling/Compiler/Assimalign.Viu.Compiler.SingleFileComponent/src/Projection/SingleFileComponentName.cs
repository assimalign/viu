namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>The resolved names for a generated component: its namespace (or <see langword="null"/>), class, and hint name.</summary>
/// <param name="Namespace">The containing namespace, or <see langword="null"/> for the global namespace.</param>
/// <param name="ClassName">The generated partial class name.</param>
/// <param name="HintName">The stable <c>AddSource</c> hint name, unique by construction (a path hash disambiguates out-of-project files, lossy sanitizations, and names that collide with a sibling component's only by case).</param>
public readonly record struct SingleFileComponentName(string? Namespace, string ClassName, string HintName);
