namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>
/// One property written on a component usage in a template, reduced to exactly what build-time
/// validation needs: the argument name it supplies, how it supplies it, what the checker can decide
/// about the supplied value's type, and where to put the squiggle. Value-equatable, so the usage
/// manifest rides inside the incremental generator's cached projection result.
/// </summary>
/// <param name="Name">The argument name this property supplies, already normalized to its camel-case spelling.</param>
/// <param name="AuthoredName">The name exactly as authored, for the diagnostic message.</param>
/// <param name="Kind">How the value reaches the component.</param>
/// <param name="ValueKind">What the checker can decide about the supplied value's type; <see cref="ComponentValueKind.Unknown"/> whenever it cannot.</param>
/// <param name="Location">The already file-composed source range of the property name.</param>
public readonly record struct ComponentUsageProperty(
    string Name,
    string AuthoredName,
    ComponentUsagePropertyKind Kind,
    ComponentValueKind ValueKind,
    LocationInfo Location);
