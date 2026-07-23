using System;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// A transform that rewrites the node it is applied to, driven by a matched structural directive. The C#
/// port of Vue 3.5's <c>StructuralDirectiveTransform</c> (<c>@vue/compiler-core</c> <c>transform.ts</c>).
/// Only <c>v-if</c> and <c>v-for</c> fall into this category.
/// </summary>
/// <param name="element">The element carrying the directive, with the matched directive already removed.</param>
/// <param name="directive">The matched structural directive.</param>
/// <param name="context">The active transform context.</param>
/// <returns>An exit callback, or <see langword="null"/>.</returns>
internal delegate Action? StructuralDirectiveTransform(
    ElementNode element,
    DirectiveNode directive,
    TransformContext context);

/// <summary>
/// Builds a <see cref="NodeTransform"/> from a structural directive transform, matching directives by name
/// and removing them before applying so the transform can re-traverse the node. The C# port of Vue 3.5's
/// <c>createStructuralDirectiveTransform</c> (<c>@vue/compiler-core</c> <c>transform.ts</c>).
/// </summary>
internal static class StructuralDirectiveFactory
{
    public static NodeTransform Create(Func<string, bool> matches, StructuralDirectiveTransform transform)
        => (node, context) =>
        {
            if (node is not ElementNode element)
            {
                return null;
            }

            // Structural directives are not concerned with slots; those are handled by the slot transform.
            if (element.ElementType == ElementType.Template && HasVSlot(element))
            {
                return null;
            }

            var properties = element.Properties;
            for (var index = 0; index < properties.Count; index++)
            {
                if (properties[index] is DirectiveNode directive && matches(directive.Name))
                {
                    // Remove the structural directive before applying so the node can traverse itself.
                    var reduced = RemoveProperty(element, index);
                    return transform(reduced, directive, context);
                }
            }

            return null;
        };

    private static bool HasVSlot(ElementNode element)
    {
        foreach (var property in element.Properties)
        {
            if (property is DirectiveNode { Name: "slot" })
            {
                return true;
            }
        }

        return false;
    }

    private static ElementNode RemoveProperty(ElementNode element, int index)
    {
        var remaining = new PropertyNode[element.Properties.Count - 1];
        var target = 0;
        for (var source = 0; source < element.Properties.Count; source++)
        {
            if (source == index)
            {
                continue;
            }

            remaining[target++] = element.Properties[source];
        }

        return element with { Properties = new SyntaxList<PropertyNode>(remaining) };
    }
}
