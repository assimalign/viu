using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Generators.Syntax.Tests;

/// <summary>
/// Pins the generated member-surface stability required for structural metadata deltas by
/// [V01.01.06.14] and <c>[SFC-CG-9]</c>.
/// </summary>
public sealed class SingleFileComponentEditAndContinueTests
{
    private const string ComponentName = "StructuralProbe";
    private const string ProjectDirectory = "C:/project";
    private const string RootNamespace = "Demo";

    [Theory]
    [InlineData(null)]
    [InlineData("true")]
    public void StructuralTemplateEdits_PreserveGeneratedMemberSurfaceAndFieldInitializers(
        string? serverRendering)
    {
        // Names, signatures, kinds, order, and attributes are the EnC contract. Member bodies may
        // change; static field initializers may not, because the existing type initializer has
        // already run.
        IReadOnlyList<(string Name, string Before, string After)> edits =
        [
            (
                "add element",
                "<main><h1>{{ Title }}</h1><p>stable</p></main>",
                "<main><h1>{{ Title }}</h1><p>stable</p><aside>added</aside></main>"),
            (
                "remove element",
                "<main><h1>{{ Title }}</h1><p>stable</p><aside>removed</aside></main>",
                "<main><h1>{{ Title }}</h1><p>stable</p></main>"),
            (
                "add v-if element",
                "<main><h1>{{ Title }}</h1><p>stable</p></main>",
                "<main><h1>{{ Title }}</h1><p>stable</p>" +
                "<aside v-if=\"Count > 0\">conditional</aside></main>"),
        ];

        foreach ((string name, string beforeTemplate, string afterTemplate) in edits)
        {
            string before = Generate(beforeTemplate, serverRendering);
            string after = Generate(afterTemplate, serverRendering);
            string profile = serverRendering is null ? "client" : "client/server";

            GeneratedMemberSurface(after).ShouldBe(
                GeneratedMemberSurface(before),
                customMessage: $"The {name} edit changed the {profile} generated member surface.");
            ContractFieldInitializer(after).ShouldBe(
                ContractFieldInitializer(before),
                customMessage: $"The {name} edit changed the {profile} executed contract field initializer.");
            ContractFieldInitializer(after).ShouldContain("__ViuGetRenderCacheSize");
            if (!string.Equals(name, "add v-if element", StringComparison.Ordinal))
            {
                RenderCacheSizeBody(after).ShouldNotBe(
                    RenderCacheSizeBody(before),
                    customMessage: $"The {name} edit did not move its changed cache count into the provider body.");
            }

            after.ShouldNotBe(
                before,
                customMessage: $"The {name} {profile} fixture did not produce a generated body delta.");
        }
    }

    private static string Generate(string template, string? serverRendering)
    {
        string source =
            "<template>\n" +
            template + "\n" +
            "</template>\n\n" +
            "@script {\n" +
            "    public string Title => \"title\";\n" +
            "    public int Count => 1;\n" +
            "}\n";
        GeneratorOutcome outcome = GeneratorTestHarness.Run(
            ProjectDirectory + "/" + ComponentName + ".viu",
            source,
            RootNamespace,
            ProjectDirectory,
            configuration: "Debug",
            serverRendering: serverRendering);

        outcome.Diagnostics.ShouldBeEmpty();
        return GeneratorTestHarness.GeneratedSource(
            outcome,
            ComponentName + ".SingleFileComponent.g.cs");
    }

    private static string GeneratedMemberSurface(string generatedSource)
    {
        CompilationUnitSyntax root = CSharpSyntaxTree.ParseText(generatedSource)
            .GetCompilationUnitRoot();
        ClassDeclarationSyntax component = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Single(declaration => declaration.Identifier.ValueText == ComponentName);
        ClassDeclarationSyntax surface = (ClassDeclarationSyntax)new MemberSurfaceRewriter()
            .Visit(component)!;
        return surface.WithoutTrivia().NormalizeWhitespace().ToFullString();
    }

    private static string ContractFieldInitializer(string generatedSource)
    {
        CompilationUnitSyntax root = CSharpSyntaxTree.ParseText(generatedSource)
            .GetCompilationUnitRoot();
        EqualsValueClauseSyntax initializer = root.DescendantNodes()
            .OfType<VariableDeclaratorSyntax>()
            .Single(variable => variable.Identifier.ValueText == "__ViuContract")
            .Initializer!;
        return initializer.Value.WithoutTrivia().NormalizeWhitespace().ToFullString();
    }

    private static string RenderCacheSizeBody(string generatedSource)
    {
        CompilationUnitSyntax root = CSharpSyntaxTree.ParseText(generatedSource)
            .GetCompilationUnitRoot();
        MethodDeclarationSyntax method = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(declaration => declaration.Identifier.ValueText == "__ViuGetRenderCacheSize");
        return method.Body!.WithoutTrivia().NormalizeWhitespace().ToFullString();
    }

    private sealed class MemberSurfaceRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node) =>
            node.WithBody(null)
                .WithExpressionBody(null)
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

        public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node) =>
            node.WithBody(null)
                .WithExpressionBody(null)
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

        public override SyntaxNode? VisitDestructorDeclaration(DestructorDeclarationSyntax node) =>
            node.WithBody(null)
                .WithExpressionBody(null)
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

        public override SyntaxNode? VisitOperatorDeclaration(OperatorDeclarationSyntax node) =>
            node.WithBody(null)
                .WithExpressionBody(null)
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

        public override SyntaxNode? VisitConversionOperatorDeclaration(
            ConversionOperatorDeclarationSyntax node) =>
            node.WithBody(null)
                .WithExpressionBody(null)
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

        public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            AccessorListSyntax? accessorList = WithoutBodies(node.AccessorList);
            return node.WithAccessorList(accessorList)
                .WithExpressionBody(null)
                .WithSemicolonToken(accessorList is null || node.Initializer is not null
                    ? SyntaxFactory.Token(SyntaxKind.SemicolonToken)
                    : default);
        }

        public override SyntaxNode? VisitIndexerDeclaration(IndexerDeclarationSyntax node)
        {
            AccessorListSyntax? accessorList = WithoutBodies(node.AccessorList);
            return node.WithAccessorList(accessorList)
                .WithExpressionBody(null)
                .WithSemicolonToken(accessorList is null
                    ? SyntaxFactory.Token(SyntaxKind.SemicolonToken)
                    : default);
        }

        public override SyntaxNode? VisitEventDeclaration(EventDeclarationSyntax node)
        {
            AccessorListSyntax? accessorList = WithoutBodies(node.AccessorList);
            return node.WithAccessorList(accessorList)
                .WithSemicolonToken(accessorList is null
                    ? SyntaxFactory.Token(SyntaxKind.SemicolonToken)
                    : default);
        }

        private static AccessorListSyntax? WithoutBodies(AccessorListSyntax? accessorList)
        {
            if (accessorList is null)
            {
                return null;
            }

            SyntaxList<AccessorDeclarationSyntax> accessors = accessorList.Accessors;
            for (int index = 0; index < accessors.Count; index++)
            {
                AccessorDeclarationSyntax accessor = accessors[index];
                accessors = accessors.Replace(
                    accessor,
                    accessor.WithBody(null)
                        .WithExpressionBody(null)
                        .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
            }

            return accessorList.WithAccessors(accessors);
        }
    }
}
