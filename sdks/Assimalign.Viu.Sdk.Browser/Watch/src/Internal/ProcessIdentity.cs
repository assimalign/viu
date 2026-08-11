namespace Assimalign.Viu.Sdk.CssHotReload;

internal readonly record struct ProcessIdentity(
    int ProcessIdentifier,
    long? StartTimeUtcTicks);
