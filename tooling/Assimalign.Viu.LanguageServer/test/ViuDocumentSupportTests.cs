using System;
using System.IO;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageServer.Tests;

public class ViuDocumentSupportTests
{
    [Fact]
    public void IsSupported_ViuDocument_DoesNotRequireProjectDiscovery()
    {
        ViuDocumentSupport
            .IsSupported("file:///workspace/Component.viu")
            .ShouldBeTrue();
    }

    [Fact]
    public void IsSupported_VueDocumentUnderViuProject_IsEnabled()
    {
        WithTemporaryDirectory(
            directory =>
            {
                File.WriteAllText(
                    Path.Combine(directory, "Application.csproj"),
                    "<Project Sdk=\"Assimalign.Viu.Sdk\" />");
                var componentPath = Path.Combine(
                    directory,
                    "Components",
                    "Card.vue");
                Directory.CreateDirectory(Path.GetDirectoryName(componentPath)!);
                File.WriteAllText(componentPath, "<template />");

                ViuDocumentSupport
                    .IsSupported(new Uri(componentPath).AbsoluteUri)
                    .ShouldBeTrue();
            });
    }

    [Fact]
    public void IsSupported_VueDocumentUnderViuBrowserProject_IsEnabled()
    {
        // [V01.01.12.27] Browser applications use the browser SDK while the
        // base SDK remains the component-library entry point.
        WithTemporaryDirectory(
            directory =>
            {
                File.WriteAllText(
                    Path.Combine(directory, "Application.csproj"),
                    "<Project Sdk=\"Assimalign.Viu.Sdk.Browser\" />");
                var componentPath = Path.Combine(
                    directory,
                    "Components",
                    "Card.vue");
                Directory.CreateDirectory(Path.GetDirectoryName(componentPath)!);
                File.WriteAllText(componentPath, "<template />");

                ViuDocumentSupport
                    .IsSupported(new Uri(componentPath).AbsoluteUri)
                    .ShouldBeTrue();
            });
    }

    [Fact]
    public void IsSupported_VueDocumentUnderNearestUnrelatedProject_DoesNotUseAncestorViuProject()
    {
        WithTemporaryDirectory(
            directory =>
            {
                File.WriteAllText(
                    Path.Combine(directory, "Application.csproj"),
                    "<Project Sdk=\"Assimalign.Viu.Sdk\" />");
                var nestedDirectory = Path.Combine(directory, "WebClient");
                Directory.CreateDirectory(nestedDirectory);
                File.WriteAllText(
                    Path.Combine(nestedDirectory, "WebClient.csproj"),
                    "<Project Sdk=\"Microsoft.NET.Sdk\" />");
                var componentPath = Path.Combine(nestedDirectory, "Card.vue");
                File.WriteAllText(componentPath, "<template />");

                ViuDocumentSupport
                    .IsSupported(new Uri(componentPath).AbsoluteUri)
                    .ShouldBeFalse();
            });
    }

    [Fact]
    public void IsSupported_VueDocumentInMixedSolution_UsesItsOwnProject()
    {
        WithTemporaryDirectory(
            directory =>
            {
                var viuDirectory = Path.Combine(directory, "ViuApplication");
                Directory.CreateDirectory(viuDirectory);
                File.WriteAllText(
                    Path.Combine(viuDirectory, "ViuApplication.csproj"),
                    "<Project Sdk=\"Assimalign.Viu.Sdk\" />");

                var vueDirectory = Path.Combine(directory, "VueApplication");
                Directory.CreateDirectory(vueDirectory);
                File.WriteAllText(
                    Path.Combine(vueDirectory, "VueApplication.csproj"),
                    "<Project Sdk=\"Microsoft.NET.Sdk\" />");
                var componentPath = Path.Combine(vueDirectory, "Card.vue");
                File.WriteAllText(componentPath, "<template />");

                ViuDocumentSupport
                    .IsSupported(new Uri(componentPath).AbsoluteUri)
                    .ShouldBeFalse();
            });
    }

    [Fact]
    public void IsSupported_VueDocumentWithCollocatedMixedProjects_FailsClosed()
    {
        WithTemporaryDirectory(
            directory =>
            {
                File.WriteAllText(
                    Path.Combine(directory, "ViuApplication.csproj"),
                    "<Project Sdk=\"Assimalign.Viu.Sdk\" />");
                File.WriteAllText(
                    Path.Combine(directory, "VueApplication.csproj"),
                    "<Project Sdk=\"Microsoft.NET.Sdk\" />");
                var componentPath = Path.Combine(directory, "Card.vue");
                File.WriteAllText(componentPath, "<template />");

                ViuDocumentSupport
                    .IsSupported(new Uri(componentPath).AbsoluteUri)
                    .ShouldBeFalse();
            });
    }

    [Fact]
    public void IsSupported_VueDocumentWithExplicitProjectMarker_IsEnabled()
    {
        WithTemporaryDirectory(
            directory =>
            {
                File.WriteAllText(
                    Path.Combine(directory, "Application.csproj"),
                    """
                    <Project Sdk="Microsoft.NET.Sdk">
                      <PropertyGroup>
                        <ViuVisualStudioLanguageServiceEnabled>true</ViuVisualStudioLanguageServiceEnabled>
                      </PropertyGroup>
                    </Project>
                    """);
                var componentPath = Path.Combine(directory, "Card.vue");
                File.WriteAllText(componentPath, "<template />");

                ViuDocumentSupport
                    .IsSupported(new Uri(componentPath).AbsoluteUri)
                    .ShouldBeTrue();
            });
    }

    [Fact]
    public void IsSupported_VueDocumentUnderOptedOutViuProject_IsDisabled()
    {
        WithTemporaryDirectory(
            directory =>
            {
                File.WriteAllText(
                    Path.Combine(directory, "Application.csproj"),
                    """
                    <Project Sdk="Assimalign.Viu.Sdk">
                      <PropertyGroup>
                        <ViuVisualStudioLanguageServiceEnabled>false</ViuVisualStudioLanguageServiceEnabled>
                      </PropertyGroup>
                    </Project>
                    """);
                var componentPath = Path.Combine(directory, "Card.vue");
                File.WriteAllText(componentPath, "<template />");

                ViuDocumentSupport
                    .IsSupported(new Uri(componentPath).AbsoluteUri)
                    .ShouldBeFalse();
            });
    }

    private static void WithTemporaryDirectory(Action<string> action)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "viu-language-server-tests",
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
