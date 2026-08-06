namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// A script block — the component's C# body, merged into its partial class. The C# is <em>not</em>
/// analysed here: the container parse only slices, and Roslyn analysis of the content happens
/// downstream ([V01.01.06.03]).
/// </summary>
public sealed record SingleFileComponentScriptBlock : SingleFileComponentBlock
{
    /// <inheritdoc />
    public override SingleFileComponentBlockKind Kind => SingleFileComponentBlockKind.Script;
}
