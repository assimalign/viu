using System;
using System.Text;

using Assimalign.Viu;

namespace Assimalign.Viu.Testing;

/// <summary>
/// The auto-generated stub for a stubbed child component — the C# port of <c>@vue/test-utils</c>'s
/// default stub (https://test-utils.vuejs.org/guide/advanced/stubs-shallow-mount.html). Renders a
/// recognizable placeholder element (<c>&lt;{kebab-name}-stub&gt;</c>) instead of the real
/// component's subtree, with no reflection-based proxy generation — the stub is a plain component
/// definition returning a fixed render function.
/// </summary>
internal sealed class StubComponent : IComponent
{
    private readonly ComponentSetup _render;

    private StubComponent(string tag)
    {
        Name = tag;
        var placeholder = VirtualNodeFactory.Element(tag);
        _render = () => placeholder;
    }

    public string? Name { get; }

    public ComponentSetup Setup(ComponentProperties properties, ComponentSetupContext context) => _render;

    /// <summary>Creates the placeholder stub for <paramref name="real"/> (tag = kebab name + "-stub").</summary>
    /// <param name="real">The component being stubbed.</param>
    public static StubComponent For(IComponent real) => new(ToStubTag(real.Name));

    private static string ToStubTag(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "anonymous-stub";
        }
        var builder = new StringBuilder(name.Length + 6);
        for (var index = 0; index < name.Length; index++)
        {
            var character = name[index];
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
