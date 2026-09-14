using System;
using System.Linq;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Compiler.SingleFileComponent.Tests;

/// <summary>
/// Pins generated type disambiguation and preserved hint identities for sibling-layout components
/// (<c>[SFC-CG-10]</c>, <c>[SFC-CG-5]</c>, [V01.01.06.16]).
/// </summary>
public sealed class SingleFileComponentNameResolverTests
{
    private const string ProjectDirectory = "C:/project";
    private const string RootNamespace = "Root";

    [Theory]
    [InlineData(".viu")]
    [InlineData(".vue")]
    public void Resolve_SiblingLayout_MovesOnlyLayoutAndPreservesClassAndHint(string extension)
    {
        var layout = ProjectDirectory + "/Pages/Blog" + extension;
        var entry = ProjectDirectory + "/Pages/Blog/Entry" + extension;
        var paths = new[] { layout, entry };

        var selected = SelectNamespaces(paths);
        var layoutName = Resolve(layout, selected);
        var entryName = Resolve(entry, selected);

        selected.ShouldBe(new[] { layout });
        layoutName.Namespace.ShouldBe("Root.Pages.GeneratedComponents");
        layoutName.ClassName.ShouldBe("Blog");
        layoutName.HintName.ShouldBe("Pages.Blog.SingleFileComponent.g.cs");
        entryName.ShouldBe(SingleFileComponentNameResolver.Resolve(entry, ProjectDirectory, RootNamespace));
        SelectIdentities(paths).ShouldBeEmpty();
    }

    [Fact]
    public void SelectNamespaceCollidingPaths_DeepAncestors_SelectsEveryConflictingLayout()
    {
        const string blog = ProjectDirectory + "/Pages/Blog.viu";
        const string archive = ProjectDirectory + "/Pages/Blog/Archive.viu";
        var paths = new[] { blog, archive, ProjectDirectory + "/Pages/Blog/Archive/Years/Entry.vue" };

        var selected = SelectNamespaces(paths);

        selected.ShouldBe(new[] { blog, archive });
        Resolve(blog, selected).Namespace.ShouldBe("Root.Pages.GeneratedComponents");
        Resolve(archive, selected).Namespace.ShouldBe("Root.Pages.Blog.GeneratedComponents");
        SelectIdentities(paths).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Foo-Bar", "Foo_Bar", "Foo_Bar")]
    [InlineData("Foo_Bar", "Foo-Bar", "Foo_Bar")]
    [InlineData("123", "123", "_123")]
    public void SelectNamespaceCollidingPaths_SanitizedNames_UsesEmittedIdentifiers(
        string fileBaseName,
        string directoryName,
        string expectedClassName)
    {
        var layout = ProjectDirectory + "/Pages/" + fileBaseName + ".viu";
        var paths = new[] { layout, ProjectDirectory + "/Pages/" + directoryName + "/Entry.vue" };

        var selected = SelectNamespaces(paths);
        var name = Resolve(layout, selected);

        selected.ShouldBe(new[] { layout });
        name.Namespace.ShouldBe("Root.Pages.GeneratedComponents");
        name.ClassName.ShouldBe(expectedClassName);
        name.HintName.ShouldBe(SingleFileComponentNameResolver.Resolve(layout, ProjectDirectory, RootNamespace).HintName);
        SelectIdentities(paths).ShouldBeEmpty();
    }

    [Fact]
    public void SelectNamespaceCollidingPaths_CaseOnlyDifference_DoesNotChangeCaseSensitiveTypes()
    {
        var paths = new[] { ProjectDirectory + "/Pages/Blog.viu", ProjectDirectory + "/Pages/blog/Entry.viu" };

        SelectNamespaces(paths).ShouldBeEmpty();
        SelectIdentities(paths).ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_KeywordNamesAndEscapedRootNamespace_PreservesCSharpEscapes()
    {
        const string layout = ProjectDirectory + "/Pages/class.viu";
        const string escapedRootNamespace = "@namespace.@Root";
        var paths = new[] { layout, ProjectDirectory + "/Pages/class/Entry.vue" };

        var selected = SingleFileComponentNameResolver.SelectNamespaceCollidingPaths(paths, ProjectDirectory, escapedRootNamespace);
        var name = SingleFileComponentNameResolver.Resolve(layout, ProjectDirectory, escapedRootNamespace, false, true);

        selected.ShouldBe(new[] { layout });
        name.Namespace.ShouldBe("@namespace.@Root.Pages.GeneratedComponents");
        name.ClassName.ShouldBe("@class");
        name.HintName.ShouldBe("Pages.class.SingleFileComponent.g.cs");
        SingleFileComponentNameResolver.SelectIdentityCollidingPaths(paths, ProjectDirectory, escapedRootNamespace).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Resolve_GlobalNamespace_CreatesGeneratedChildNamespace(string? rootNamespace)
    {
        const string layout = ProjectDirectory + "/Blog.viu";
        var paths = new[] { layout, ProjectDirectory + "/Blog/Entry.viu" };

        SingleFileComponentNameResolver.SelectNamespaceCollidingPaths(paths, ProjectDirectory, rootNamespace).ShouldBe(new[] { layout });
        var name = SingleFileComponentNameResolver.Resolve(layout, ProjectDirectory, rootNamespace, false, true);

        name.Namespace.ShouldBe("GeneratedComponents");
        name.ClassName.ShouldBe("Blog");
        name.HintName.ShouldBe("Blog.SingleFileComponent.g.cs");
        SingleFileComponentNameResolver.SelectIdentityCollidingPaths(paths, ProjectDirectory, rootNamespace).ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_WindowsSeparators_UsesSameGeneratedIdentity()
    {
        const string layout = "C:\\project\\Pages\\Blog.viu";
        var paths = new[] { layout, "C:\\project\\Pages\\Blog\\Entry.viu" };

        var selected = SelectNamespaces(paths);

        selected.ShouldBe(new[] { layout });
        Resolve(layout, selected).ShouldBe(SingleFileComponentNameResolver.Resolve(
            ProjectDirectory + "/Pages/Blog.viu", ProjectDirectory, RootNamespace, false, true));
    }

    [Fact]
    public void Resolve_UnrelatedComponents_KeepsExistingOverloadsAndHintDiscriminatorIndependent()
    {
        const string layout = ProjectDirectory + "/Pages/Blog.viu";
        const string unrelated = ProjectDirectory + "/Components/Counter.viu";
        var paths = new[] { unrelated, layout, ProjectDirectory + "/Pages/Blog/Entry.viu" };
        var selected = SelectNamespaces(paths);

        Resolve(unrelated, selected).ShouldBe(SingleFileComponentNameResolver.Resolve(unrelated, ProjectDirectory, RootNamespace));
        foreach (var requiresCaseDiscriminator in new[] { false, true })
        {
            var legacy = SingleFileComponentNameResolver.Resolve(layout, ProjectDirectory, RootNamespace, requiresCaseDiscriminator);
            var relocated = SingleFileComponentNameResolver.Resolve(layout, ProjectDirectory, RootNamespace, requiresCaseDiscriminator, true);

            relocated.HintName.ShouldBe(legacy.HintName);
            relocated.ClassName.ShouldBe(legacy.ClassName);
            SingleFileComponentNameResolver.Resolve(layout, ProjectDirectory, RootNamespace, requiresCaseDiscriminator, false).ShouldBe(legacy);
        }
    }

    [Fact]
    public void SelectCollidingPaths_ReorderedInputs_ReturnsSameOrdinalSelection()
    {
        const string archive = ProjectDirectory + "/Pages/Archive.viu";
        const string blog = ProjectDirectory + "/Pages/Blog.viu";
        var paths = new[]
        {
            blog,
            ProjectDirectory + "/Pages/Blog/Entry.viu",
            archive,
            ProjectDirectory + "/Pages/Archive/Year.vue",
            ProjectDirectory + "/Pages/GeneratedComponents/Blog.vue"
        };
        var reversed = paths.Reverse().ToArray();

        SelectNamespaces(paths).ShouldBe(new[] { archive, blog });
        SelectNamespaces(reversed).ShouldBe(SelectNamespaces(paths));
        SelectIdentities(reversed).ShouldBe(SelectIdentities(paths));
        foreach (var path in paths)
        {
            Resolve(path, SelectNamespaces(reversed)).ShouldBe(Resolve(path, SelectNamespaces(paths)));
        }
    }

    [Fact]
    public void SelectIdentityCollidingPaths_DuplicateSanitizedTypes_SelectsAllContributors()
    {
        const string punctuated = ProjectDirectory + "/Pages/Foo-Bar.viu";
        const string underscored = ProjectDirectory + "/Pages/Foo_Bar.vue";
        var paths = new[] { underscored, ProjectDirectory + "/Pages/Counter.viu", punctuated };

        SelectNamespaces(paths).ShouldBeEmpty();
        SelectIdentities(paths).ShouldBe(new[] { punctuated, underscored });
    }

    [Fact]
    public void SelectIdentityCollidingPaths_SanitizedDirectories_SelectsDuplicateFinalTypes()
    {
        const string punctuated = ProjectDirectory + "/Foo-Bar/Entry.viu";
        const string underscored = ProjectDirectory + "/Foo_Bar/Entry.viu";

        SelectIdentities(new[] { underscored, punctuated }).ShouldBe(new[] { punctuated, underscored });
    }

    [Fact]
    public void SelectIdentityCollidingPaths_OutsideProjectNames_SelectsDuplicateGlobalTypes()
    {
        var paths = new[] { "C:/linked/First/Button.viu", "C:/linked/Second/Button.vue" };

        SelectNamespaces(paths).ShouldBeEmpty();
        SingleFileComponentNameResolver.SelectIdentityCollidingPaths(paths, ProjectDirectory, null).ShouldBe(paths);
    }

    [Fact]
    public void SelectIdentityCollidingPaths_CaseDistinctTypes_DoesNotRejectLegalDeclarations()
    {
        var paths = new[] { ProjectDirectory + "/Pages/Choice.viu", ProjectDirectory + "/Pages/choice.viu" };

        SelectIdentities(paths).ShouldBeEmpty();
        SingleFileComponentNameResolver.SelectCaseCollidingPaths(paths, ProjectDirectory).ShouldBe(paths);
    }

    [Fact]
    public void SelectIdentityCollidingPaths_GeneratedLeafDuplicateType_SelectsOnlyFinalCollisionParticipants()
    {
        const string layout = ProjectDirectory + "/Pages/Blog.viu";
        const string existing = ProjectDirectory + "/Pages/GeneratedComponents/Blog.vue";
        var paths = new[] { layout, ProjectDirectory + "/Pages/Blog/Entry.viu", existing };

        SelectNamespaces(paths).ShouldBe(new[] { layout });
        SelectIdentities(paths).ShouldBe(new[] { layout, existing });
    }

    [Fact]
    public void SelectIdentityCollidingPaths_GeneratedLeafNamespace_SelectsTypeAndEveryNamespaceContributor()
    {
        const string layout = ProjectDirectory + "/Pages/Blog.viu";
        const string archive = ProjectDirectory + "/Pages/GeneratedComponents/Blog/Archive.vue";
        const string entry = ProjectDirectory + "/Pages/GeneratedComponents/Blog/Entry.viu";
        var paths = new[] { layout, ProjectDirectory + "/Pages/Blog/Overview.viu", entry, archive };

        SelectNamespaces(paths).ShouldBe(new[] { layout });
        SelectIdentities(paths).ShouldBe(new[] { layout, archive, entry });
    }

    [Fact]
    public void SelectIdentityCollidingPaths_GeneratedLeafIntroducesNamespaceConflict_SelectsBothContributors()
    {
        const string layout = ProjectDirectory + "/Pages/Blog.viu";
        const string generatedComponents = ProjectDirectory + "/Pages/GeneratedComponents.viu";
        var paths = new[] { layout, ProjectDirectory + "/Pages/Blog/Entry.viu", generatedComponents };

        SelectNamespaces(paths).ShouldBe(new[] { layout });
        SelectIdentities(paths).ShouldBe(new[] { layout, generatedComponents });
    }

    [Fact]
    public void SelectIdentityCollidingPaths_GeneratedComponentsLayoutWithoutFurtherConflict_Compiles()
    {
        const string layout = ProjectDirectory + "/Pages/GeneratedComponents.viu";
        var paths = new[] { layout, ProjectDirectory + "/Pages/GeneratedComponents/Entry.viu" };

        SelectNamespaces(paths).ShouldBe(new[] { layout });
        Resolve(layout, SelectNamespaces(paths)).Namespace.ShouldBe("Root.Pages.GeneratedComponents");
        SelectIdentities(paths).ShouldBeEmpty();
    }

    [Fact]
    public void SelectIdentityCollidingPaths_GeneratedComponentsLayoutWithRepeatedSegment_SelectsResidualConflict()
    {
        const string layout = ProjectDirectory + "/Pages/GeneratedComponents.viu";
        const string entry = ProjectDirectory + "/Pages/GeneratedComponents/GeneratedComponents/Entry.viu";
        var paths = new[] { layout, entry };

        SelectNamespaces(paths).ShouldBe(new[] { layout });
        SelectIdentities(paths).ShouldBe(paths);
    }

    [Fact]
    public void SelectCollidingPaths_NoComponents_ReturnsEmptySelections()
    {
        SelectNamespaces(Array.Empty<string>()).ShouldBeEmpty();
        SelectIdentities(Array.Empty<string>()).ShouldBeEmpty();
    }

    private static string[] SelectNamespaces(string[] paths)
        => SingleFileComponentNameResolver.SelectNamespaceCollidingPaths(paths, ProjectDirectory, RootNamespace);

    private static string[] SelectIdentities(string[] paths)
        => SingleFileComponentNameResolver.SelectIdentityCollidingPaths(paths, ProjectDirectory, RootNamespace);

    private static SingleFileComponentName Resolve(string path, string[] namespaceCollidingPaths)
        => SingleFileComponentNameResolver.Resolve(
            path,
            ProjectDirectory,
            RootNamespace,
            requiresCaseDiscriminator: false,
            requiresNamespaceDiscriminator: namespaceCollidingPaths.Contains(path, StringComparer.Ordinal));
}
