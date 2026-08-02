using System;
using System.IO;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageServer;

public sealed class ViuUtilityStylesheetContextTests
{
    [Fact]
    public void ReadForDocument_DirectProjectEntry_ReturnsUtilityStylesheet()
    {
        WithTemporaryDirectory(
            directory =>
            {
                const string stylesheet =
                    "@theme { --color-brand: #123456; }";
                File.WriteAllText(
                    Path.Combine(directory, "Application.csproj"),
                    """
                    <Project Sdk="Assimalign.Viu.Sdk">
                      <ItemGroup>
                        <ViuUtilityCss Include="Styles/Utilities.css" />
                      </ItemGroup>
                    </Project>
                    """);
                var stylesDirectory = Path.Combine(directory, "Styles");
                Directory.CreateDirectory(stylesDirectory);
                File.WriteAllText(
                    Path.Combine(stylesDirectory, "Utilities.css"),
                    stylesheet);
                var componentPath = Path.Combine(directory, "Card.vue");
                File.WriteAllText(componentPath, "<template />");

                var result = ViuUtilityStylesheetContext.ReadForDocument(
                    new Uri(componentPath).AbsoluteUri);

                result.ShouldNotBeNull();
                result.StylesheetText.ShouldBe(stylesheet);
                result.StylesheetIdentity.ShouldBe(
                    Path.Combine(
                        stylesDirectory,
                        "Utilities.css"));
                result.ReferenceGraph.References.ShouldBeEmpty();
            });
    }

    [Fact]
    public void ReadForDocument_NestedReferences_ReturnsResolvedTransitiveGraph()
    {
        WithTemporaryDirectory(
            directory =>
            {
                File.WriteAllText(
                    Path.Combine(directory, "Application.csproj"),
                    """
                    <Project Sdk="Assimalign.Viu.Sdk">
                      <ItemGroup>
                        <ViuUtilityCss Include="Styles/Utilities.css" />
                      </ItemGroup>
                    </Project>
                    """);
                var stylesDirectory = Path.Combine(directory, "Styles");
                Directory.CreateDirectory(stylesDirectory);
                File.WriteAllText(
                    Path.Combine(stylesDirectory, "Utilities.css"),
                    "@reference \"./Shared.css\";");
                File.WriteAllText(
                    Path.Combine(stylesDirectory, "Shared.css"),
                    "@utility brand-card { color: rebeccapurple; }");
                var componentPath = Path.Combine(directory, "Card.vue");
                File.WriteAllText(componentPath, "<template />");

                var result = ViuUtilityStylesheetContext.ReadForDocument(
                    new Uri(componentPath).AbsoluteUri);

                result.ShouldNotBeNull();
                var reference =
                    result.ReferenceGraph.References.ShouldHaveSingleItem();
                reference.Specifier.ShouldBe("./Shared.css");
                reference.ResolvedSourceIdentity.ShouldBe(
                    Path.Combine(
                        stylesDirectory,
                        "Shared.css"));
            });
    }

    [Fact]
    public void ReadForDocument_UnresolvedBuildExpression_DoesNotGuessPath()
    {
        WithTemporaryDirectory(
            directory =>
            {
                File.WriteAllText(
                    Path.Combine(directory, "Application.csproj"),
                    """
                    <Project Sdk="Assimalign.Viu.Sdk">
                      <ItemGroup>
                        <ViuUtilityCss Include="$(UtilityRoot)/Utilities.css" />
                      </ItemGroup>
                    </Project>
                    """);
                var componentPath = Path.Combine(directory, "Card.viu");
                File.WriteAllText(componentPath, "<template><div /></template>");

                ViuUtilityStylesheetContext
                    .ReadForDocument(new Uri(componentPath).AbsoluteUri)
                    .ShouldBeNull();
            });
    }

    private static void WithTemporaryDirectory(Action<string> action)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "viu-utility-stylesheet-context-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            action(directory);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
