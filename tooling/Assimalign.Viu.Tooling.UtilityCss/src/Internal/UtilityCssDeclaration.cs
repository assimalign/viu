namespace Assimalign.Viu.Tooling.UtilityCss;

internal sealed class UtilityCssDeclaration
{
    public UtilityCssDeclaration(
        string property,
        string value)
    {
        Property = property;
        Value = value;
    }

    public string Property { get; }

    public string Value { get; }
}
