using System;
using System.Text;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Testing;

internal sealed class StubComponent : IComponent
{
    private readonly string _tag;

    private StubComponent(string tag)
    {
        _tag = tag;
    }

    public ComponentRenderer Setup(ComponentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _ => new ElementNode(new QualifiedName(_tag));
    }

    internal static StubComponent For(Type componentType)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        return new StubComponent(ToStubTag(componentType.Name));
    }

    private static string ToStubTag(string name)
    {
        if (name.Length == 0)
        {
            return "anonymous-stub";
        }

        StringBuilder builder = new(name.Length + 6);
        for (int index = 0; index < name.Length; index++)
        {
            char character = name[index];
            if (char.IsUpper(character))
            {
                if (index > 0)
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(character));
            }
            else
            {
                builder.Append(character);
            }
        }

        builder.Append("-stub");
        return builder.ToString();
    }
}
