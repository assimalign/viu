using System.Collections.Generic;

namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// Freezes the transform's mutable working containers (<see cref="WorkingIf"/>, <see cref="WorkingFor"/>,
/// <see cref="WorkingIfBranch"/>) into their immutable, value-equatable public counterparts
/// (<see cref="IfNode"/>, <see cref="ForNode"/>, <see cref="IfBranchNode"/>) as they are consumed. Elements
/// and leaf nodes are already immutable and pass through unchanged; their code-generation results stay in the
/// context's side table. This is what upholds the "immutable and value-equatable after transform" contract.
/// </summary>
internal static class TransformFreeze
{
    public static SyntaxList<TemplateChildNode> FreezeChildren(IReadOnlyList<TemplateSyntaxNode> children)
    {
        if (children.Count == 0)
        {
            return SyntaxList<TemplateChildNode>.Empty;
        }

        var array = new TemplateChildNode[children.Count];
        for (var index = 0; index < children.Count; index++)
        {
            array[index] = FreezeNode(children[index]);
        }

        return new SyntaxList<TemplateChildNode>(array);
    }

    public static TemplateChildNode FreezeNode(TemplateSyntaxNode node) => node switch
    {
        WorkingIf workingIf => new IfNode
        {
            Branches = FreezeBranches(workingIf.Branches),
            CodegenNode = workingIf.CodegenNode,
            Location = workingIf.Location,
        },
        WorkingFor workingFor => new ForNode
        {
            Source = workingFor.Source,
            ValueAlias = workingFor.ValueAlias,
            KeyAlias = workingFor.KeyAlias,
            ObjectIndexAlias = workingFor.ObjectIndexAlias,
            ParseResult = workingFor.ParseResult,
            Children = FreezeChildren(workingFor.Children),
            CodegenNode = workingFor.CodegenNode,
            Location = workingFor.Location,
        },
        TemplateChildNode templateChild => templateChild,
        _ => (TemplateChildNode)node,
    };

    private static SyntaxList<IfBranchNode> FreezeBranches(IReadOnlyList<WorkingIfBranch> branches)
    {
        var array = new IfBranchNode[branches.Count];
        for (var index = 0; index < branches.Count; index++)
        {
            var branch = branches[index];
            array[index] = new IfBranchNode
            {
                Condition = branch.Condition,
                Children = FreezeChildren(branch.Children),
                UserKey = branch.UserKey,
                IsTemplateIf = branch.IsTemplateIf,
                Location = branch.Location,
            };
        }

        return new SyntaxList<IfBranchNode>(array);
    }
}
