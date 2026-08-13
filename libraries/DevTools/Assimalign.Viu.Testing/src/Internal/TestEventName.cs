using System;

namespace Assimalign.Viu.Testing;

internal readonly record struct TestEventName(string EventName, bool Once)
{
    internal static TestEventName Parse(string rawName)
    {
        ArgumentException.ThrowIfNullOrEmpty(rawName);
        string name = rawName.Length > 2
            && rawName.StartsWith("on", StringComparison.Ordinal)
            && char.IsUpper(rawName[2])
            ? rawName[2..]
            : rawName;
        bool once = false;
        bool capture = false;
        bool passive = false;
        while (true)
        {
            if (!once && name.EndsWith("Once", StringComparison.Ordinal))
            {
                name = name[..^4];
                once = true;
                continue;
            }

            if (!capture && name.EndsWith("Capture", StringComparison.Ordinal))
            {
                name = name[..^7];
                capture = true;
                continue;
            }

            if (!passive && name.EndsWith("Passive", StringComparison.Ordinal))
            {
                name = name[..^7];
                passive = true;
                continue;
            }

            break;
        }

        return new TestEventName(name.ToLowerInvariant(), once);
    }
}
