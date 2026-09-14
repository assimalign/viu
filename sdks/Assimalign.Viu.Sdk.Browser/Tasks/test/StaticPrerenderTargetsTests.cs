using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Sdk.Tasks;

namespace Assimalign.Viu.Sdk.Browser.Tasks.Tests;

/// <summary>Exercises the packaged target file with a real, isolated downstream executable (#68).</summary>
public sealed class StaticPrerenderTargetsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Publish_StaticWebRoot_PassesPublishedHostAndEveryLiteralArgument(bool browserLayout)
    {
        using var project = new TargetProject(browserLayout: browserLayout);
        const string route = "/guide/intro?text=\"quoted\"&literal=$(literal)|<value>#part";
        project.WriteProject(routes: "/;" + route.Replace("$", "%24") + ";/guide/search?q=term#part", routesFile: true);

        (int exitCode, string output) = await project.RunAsync();

        exitCode.ShouldBe(0, output);
        string[] arguments = File.ReadAllLines(Path.Combine(project.WebRoot, "arguments.txt"));
        arguments.ShouldBe(new[]
        {
            "prerender", "--output", Path.TrimEndingDirectorySeparator(project.WebRoot) + Path.DirectorySeparatorChar,
            "--host-page", Path.Combine(project.WebRoot, "index.html"),
            "--base", "/docs/", "--route", "/", "--route", route,
            "--route", "/guide/search?q=term#part",
            "--routes", Path.Combine(project.DirectoryPath, "routes file.txt"),
        });
        output.ShouldContain("Emitted /guide/intro -> guide/intro/index.html");
        // The server fails its build if Browser/publish globals leak into its independent host.
        File.ReadAllText(Path.Combine(project.WebRoot, "index.html")).ShouldContain("fingerprinted.asset.js");
    }

    [Fact]
    public async Task Publish_FailingRoute_FailsBuildAndNamesRoute()
    {
        using var project = new TargetProject();
        project.WriteProject(routes: "/fail");

        (int exitCode, string output) = await project.RunAsync();

        exitCode.ShouldNotBe(0, output);
        output.Contains("route '/fail'", StringComparison.Ordinal).ShouldBeTrue(output);
        output.Contains("deliberate failure", StringComparison.Ordinal).ShouldBeTrue(output);
    }

    [Fact]
    public async Task Publish_Disabled_DoesNotRequireConfigurationOrRunHost()
    {
        using var project = new TargetProject();
        project.WriteProject(enabled: false, configureProject: false, routes: string.Empty);

        (int exitCode, string output) = await project.RunAsync();

        exitCode.ShouldBe(0, output);
        File.Exists(Path.Combine(project.WebRoot, "arguments.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task Publish_MissingProject_ReportsConfigurationError()
    {
        using var project = new TargetProject();
        project.WriteProject(configureProject: false);

        (int exitCode, string output) = await project.RunAsync();

        exitCode.ShouldNotBe(0, output);
        output.ShouldContain("ViuStaticPrerenderProject must name a server executable project");
    }

    [Fact]
    public async Task Publish_MissingRoutes_ReportsConfigurationError()
    {
        using var project = new TargetProject();
        project.WriteProject(routes: string.Empty);

        (int exitCode, string output) = await project.RunAsync();

        exitCode.ShouldNotBe(0, output);
        output.ShouldContain("Set ViuStaticPrerenderRoutes or ViuStaticPrerenderRoutesFile");
    }

    [Fact]
    public async Task Publish_MissingPublishedHost_ReportsConfigurationError()
    {
        using var project = new TargetProject();
        project.WriteProject(writeHostPage: false);

        (int exitCode, string output) = await project.RunAsync();

        exitCode.ShouldNotBe(0, output);
        output.ShouldContain("The published static-prerender host page does not exist");
    }

    private sealed class TargetProject : IDisposable
    {
        private readonly string repositoryDirectory;

        internal TargetProject(bool browserLayout = true)
        {
            repositoryDirectory = FindRepositoryDirectory();
            DirectoryPath = Path.Combine(repositoryDirectory, "_out", "static-prerender-target-tests", Guid.NewGuid().ToString("N"));
            WebRoot = browserLayout
                ? Path.Combine(DirectoryPath, "published site", "wwwroot")
                : Path.Combine(DirectoryPath, "published site");
            Directory.CreateDirectory(Path.Combine(DirectoryPath, "Server host"));
            File.WriteAllText(Path.Combine(DirectoryPath, "Directory.Build.props"), "<Project />");
            File.WriteAllText(Path.Combine(DirectoryPath, "Directory.Build.targets"), "<Project />");
            File.WriteAllText(Path.Combine(DirectoryPath, "Server host", "Host.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                    <PropertyGroup>
                        <OutputType>Exe</OutputType>
                        <TargetFramework>net10.0</TargetFramework>
                    </PropertyGroup>
                    <Target Name="ValidateHostProperties" BeforeTargets="Build">
                        <Error Condition="'$(RuntimeIdentifier)' == 'browser-wasm' or '$(PublishTrimmed)' == 'true' or '$(ViuStaticPrerender)' == 'true'"
                               Text="Browser publish properties leaked into the server host." />
                    </Target>
                </Project>
                """);
            File.WriteAllText(Path.Combine(DirectoryPath, "Server host", "Program.cs"), """
                string output = args[System.Array.IndexOf(args, "--output") + 1];
                System.IO.Directory.CreateDirectory(output);
                System.IO.File.WriteAllLines(System.IO.Path.Combine(output, "arguments.txt"), args);
                if (System.Array.IndexOf(args, "/fail") >= 0)
                {
                    System.Console.Error.WriteLine("Static prerender failed for route '/fail': deliberate failure");
                    return 9;
                }
                System.Console.WriteLine("Emitted /guide/intro -> guide/intro/index.html");
                return 0;
                """);
        }

        internal string DirectoryPath { get; }

        internal string WebRoot { get; }

        internal void WriteProject(
            bool enabled = true,
            bool configureProject = true,
            string routes = "/",
            bool routesFile = false,
            bool writeHostPage = true)
        {
            string targetsPath = Path.Combine(repositoryDirectory, "sdks", "Assimalign.Viu.Sdk", "Targets", "Assimalign.Viu.Sdk.StaticPrerender.targets");
            if (routesFile)
            {
                File.WriteAllText(Path.Combine(DirectoryPath, "routes file.txt"), "/from-file\n");
            }

            string hostPageTarget = writeHostPage
                ? $"<MakeDir Directories=\"{Escape(WebRoot)}\" /><WriteLinesToFile File=\"{Escape(Path.Combine(WebRoot, "index.html"))}\" Lines=\"&amp;lt;script src='fingerprinted.asset.js'&amp;gt;&amp;lt;/script&amp;gt;\" Overwrite=\"true\" />"
                : string.Empty;
            File.WriteAllText(Path.Combine(DirectoryPath, "Publish.proj"), $"""
                <Project>
                    <PropertyGroup>
                        <Configuration>Release</Configuration>
                        <PublishDir>published site/</PublishDir>
                        <ViuStaticPrerender>{enabled.ToString().ToLowerInvariant()}</ViuStaticPrerender>
                        <ViuStaticPrerenderProject>{(configureProject ? "Server host/Host.csproj" : string.Empty)}</ViuStaticPrerenderProject>
                        <ViuStaticPrerenderRoutes>{Escape(routes)}</ViuStaticPrerenderRoutes>
                        <ViuStaticPrerenderRoutesFile>{(routesFile ? "routes file.txt" : string.Empty)}</ViuStaticPrerenderRoutesFile>
                        <ViuStaticPrerenderBase>/docs/</ViuStaticPrerenderBase>
                        <ViuStaticPrerenderTaskAssembly>{Escape(typeof(ViuRunStaticPrerender).Assembly.Location)}</ViuStaticPrerenderTaskAssembly>
                    </PropertyGroup>
                    <Import Project="{Escape(targetsPath)}" />
                    <Target Name="Publish">{hostPageTarget}</Target>
                </Project>
                """);
        }

        internal async Task<(int ExitCode, string Output)> RunAsync()
        {
            var startInformation = new ProcessStartInfo
            {
                FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
                WorkingDirectory = DirectoryPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (string argument in new[]
            {
                "msbuild", "Publish.proj", "-nologo", "-v:minimal", "-t:Publish",
                "-p:RuntimeIdentifier=browser-wasm", "-p:PublishTrimmed=true",
                "-p:AllowMissingPrunePackageData=true", "-nr:false",
            })
            {
                startInformation.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(startInformation)!;
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> standardError = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return (process.ExitCode, await standardOutput + Environment.NewLine + await standardError);
        }

        public void Dispose()
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }

        private static string Escape(string value) => SecurityElement.Escape(value)!;

        private static string FindRepositoryDirectory()
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Assimalign.Viu.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Could not locate the Viu repository for target tests.");
        }
    }
}
