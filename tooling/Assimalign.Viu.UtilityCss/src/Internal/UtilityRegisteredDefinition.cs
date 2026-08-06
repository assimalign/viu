namespace Assimalign.Viu.UtilityCss;

internal sealed class UtilityRegisteredDefinition
{
    public UtilityRegisteredDefinition(
        UtilityDefinition definition,
        UtilityResolverKind resolverKind)
    {
        Definition = definition;
        ResolverKind = resolverKind;
    }

    public UtilityDefinition Definition { get; }

    public UtilityResolverKind ResolverKind { get; }
}
