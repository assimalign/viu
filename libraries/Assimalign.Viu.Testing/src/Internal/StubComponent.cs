using System;
using System.Text;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Testing;

internal sealed class StubComponent : IComponentTemplate
{
    private readonly string _tag;

    private StubComponent(string tag)
    {
        _tag = tag;
    }

    public string? Name => _tag;

    public ComponentRenderer Setup(IComponentContext context)
    {
        return () => ComponentTree.Element(_tag);
    }

    internal static StubComponent For(IComponentTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        return new StubComponent(ToStubTag(template.Name));
    }

    internal static StubComponent For(Type templateType)
    {
        ArgumentNullException.ThrowIfNull(templateType);
        return new StubComponent(ToStubTag(templateType.Name));
    }

    private static string ToStubTag(string? name)
    {
        if (string.IsNullOrEmpty(name))
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
