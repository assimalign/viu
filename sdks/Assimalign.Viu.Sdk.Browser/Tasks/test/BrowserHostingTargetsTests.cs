using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Sdk.Browser.Tasks.Tests;

public sealed class BrowserHostingTargetsTests
{
    [Fact]
    public async Task ClientInfo_ReturnsAbsoluteIdentityAndManifestPathsWithoutBuilding()
    {
        // [PKG-HOST-1] A host can query an ordinary ProjectReference without publishing its client.
        using TargetProject fixture = new();
        fixture.Write("Client.proj", $$"""
            <Project>
              <PropertyGroup>
                <AssemblyName>BrowserClient</AssemblyName>
                <TargetFramework>net10.0</TargetFramework>
                <TargetPath>{{fixture.EscapePath("bin/BrowserClient.dll")}}</TargetPath>
                <TargetDir>{{fixture.EscapePath("bin/")}}</TargetDir>
              </PropertyGroup>
              <Target Name="GetTargetPath" />
              <Import Project="{{fixture.TargetPath("Assimalign.Viu.Sdk.Browser.ClientInfo.targets")}}" />
              <Target Name="Inspect" DependsOnTargets="GetViuBrowserClientInfo">
                <WriteLinesToFile File="info.txt" Overwrite="true"
                  Lines="@(_ViuBrowserClientInfo->'%(Identity)|%(IsViuBrowserApplication)|%(AssemblyName)|%(TargetPath)|%(OutputDirectory)|%(StaticWebAssetsRuntimeManifestPath)|%(StaticWebAssetsEndpointsManifestPath)')" />
              </Target>
            </Project>
            """);
        (int exitCode, string output) = await fixture.RunAsync("Client.proj");
        exitCode.ShouldBe(0, output);
        string[] fields = fixture.Read("info.txt").Trim().Split('|');
        fields[0].ShouldBe(Path.Combine(fixture.DirectoryPath, "Client.proj"));
        fields[1].ShouldBe("true");
        fields[2].ShouldBe("BrowserClient");
        fields[3].ShouldBe(Path.Combine(fixture.DirectoryPath, "bin", "BrowserClient.dll"));
        fields[5].ShouldBe(Path.Combine(fixture.DirectoryPath, "bin", "BrowserClient.staticwebassets.runtime.json"));
        fields[6].ShouldBe(Path.Combine(fixture.DirectoryPath, "bin", "BrowserClient.staticwebassets.endpoints.json"));
        Directory.Exists(Path.Combine(fixture.DirectoryPath, "bin")).ShouldBeFalse();
    }

    [Fact]
    public async Task ProjectReferenceStyles_CopyCssAndStripForeignMetadataAcrossRepeatedBuilds()
    {
        // [PKG-STYLE-1] OriginalItemSpec must not redirect compression to the referenced project.
        using TargetProject fixture = new();
        fixture.Write("library.css", ".library { color: red; }");
        fixture.Write("Library.proj", $$"""
            <Project>
              <Target Name="GetViuComponentLibraryStyles" Returns="@(LibraryStyle)">
                <ItemGroup>
                  <LibraryStyle Include="{{fixture.EscapePath("library.css")}}" PackageId="Library">
                    <OriginalItemSpec>Library.proj</OriginalItemSpec>
                  </LibraryStyle>
                </ItemGroup>
              </Target>
            </Project>
            """);
        fixture.Write("Client.proj", $$"""
            <Project>
              <PropertyGroup><UsingMicrosoftNETSdkWebAssembly>true</UsingMicrosoftNETSdkWebAssembly></PropertyGroup>
              <ItemGroup><_MSBuildProjectReferenceExistent Include="Library.proj" /></ItemGroup>
              <Target Name="ResolveProjectReferences" />
              <Import Project="{{fixture.TargetPath("Assimalign.Viu.Sdk.Browser.ComponentStyles.targets")}}" />
              <Target Name="Inspect" DependsOnTargets="ViuFlowComponentLibraryStyles">
                <Error Condition="'%(Content.OriginalItemSpec)' != ''" Text="OriginalItemSpec leaked onto CSS content." />
                <Error Condition="'%(Content.MSBuildSourceProjectFile)' != ''" Text="Source project metadata leaked onto CSS content." />
                <WriteLinesToFile File="content.txt" Lines="@(Content->'%(FullPath)')" Overwrite="true" />
              </Target>
            </Project>
            """);
        for (int iteration = 0; iteration < 2; iteration++)
        {
            (int exitCode, string output) = await fixture.RunAsync("Client.proj");
            exitCode.ShouldBe(0, output);
            fixture.Read("wwwroot/_content/Library/library.css").ShouldBe(".library { color: red; }");
            fixture.Read("content.txt").Trim().ShouldBe(Path.Combine(fixture.DirectoryPath,
                "wwwroot", "_content", "Library", "library.css"));
        }
    }

    private sealed class TargetProject : IDisposable
    {
        private readonly string repositoryDirectory;

        internal TargetProject()
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Assimalign.Viu.slnx")))
            {
                directory = directory.Parent;
            }
            repositoryDirectory = directory?.FullName ?? throw new InvalidOperationException("Repository not found.");
            DirectoryPath = Path.Combine(repositoryDirectory, "_out", "browser-hosting-target-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
        }

        internal string DirectoryPath { get; }
        internal string EscapePath(string path) => SecurityElement.Escape(Path.GetFullPath(Path.Combine(DirectoryPath, path)))!;
        internal string TargetPath(string name) => SecurityElement.Escape(Path.Combine(repositoryDirectory,
            "sdks", "Assimalign.Viu.Sdk.Browser", "Targets", name))!;
        internal void Write(string name, string content) => File.WriteAllText(Path.Combine(DirectoryPath, name), content);
        internal string Read(string name) => File.ReadAllText(Path.Combine(DirectoryPath, name));

        internal async Task<(int ExitCode, string Output)> RunAsync(string project)
        {
            ProcessStartInfo start = new(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
            {
                WorkingDirectory = DirectoryPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (string argument in new[] { "msbuild", project, "-t:Inspect", "-nologo", "-v:minimal", "-nr:false" })
            {
                start.ArgumentList.Add(argument);
            }
            using Process process = Process.Start(start)!;
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return (process.ExitCode, await output + await error);
        }

        public void Dispose() => Directory.Delete(DirectoryPath, recursive: true);
    }
}
