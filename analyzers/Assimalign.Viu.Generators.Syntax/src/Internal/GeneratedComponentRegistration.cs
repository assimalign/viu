namespace Assimalign.Viu.Generators.Syntax;

/// <summary>
/// The value-equatable identity needed to add one generated component registration to the
/// consumer-assembly catalog.
/// </summary>
/// <param name="Namespace">The component namespace, or <see langword="null"/>.</param>
/// <param name="ClassName">The generated component class name.</param>
/// <param name="FilePath">The stable source path used to order catalog entries.</param>
/// <param name="HasServerRender">Whether this component carries the project-selected server profile.</param>
internal readonly record struct GeneratedComponentRegistration(
    string? Namespace,
    string ClassName,
    string FilePath,
    bool HasServerRender);
