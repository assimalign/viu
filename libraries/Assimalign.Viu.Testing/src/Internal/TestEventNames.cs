using System;

namespace Assimalign.Viu.Testing;

internal static class TestEventNames
{
    internal static bool IsListener(string name)
    {
        return name.Length > 2
            && name.StartsWith("on", StringComparison.Ordinal)
            && char.IsUpper(name[2]);
    }
}
