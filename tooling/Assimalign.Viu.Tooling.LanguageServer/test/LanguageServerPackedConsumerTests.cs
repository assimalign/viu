using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

using Shouldly;

using Xunit;
using Xunit.Abstractions;

namespace Assimalign.Viu.Tooling.LanguageServer;

/// <summary>
/// The end-to-end packed-consumer semantic round trip ([V01.01.12.23], #259, acceptance
/// criterion 5): a hermetic consumer project scaffolded against the repo-local package feed,
/// restored once with <c>dotnet restore</c> (the exact consumer prerequisite the extension
/// DESIGN.md documents), then driven over real stdio against the published single-file server
/// executable. One session proves the three resolution layers at once: a projection-declared
/// member typed by a framework type, a sibling <c>.cs</c> partial method, and a member of a type
/// delivered through the <c>Assimalign.Viu.App.Ref</c> targeting pack
/// (<c>frameworkReferences</c>/<c>downloadDependencies</c> — the assets-resolution path that
/// <c>targets[].compile</c> alone would silently miss).
/// </summary>
/// <remarks>
/// The fact is inert without the lane: it returns immediately unless the
/// <c>VIU_PACKED_CONSUMER_FIXTURE</c> environment variable names a fixture file (see
/// <see cref="LanguageServerPackedConsumerFixture"/>), so every ordinary <c>dotnet test</c> run
/// stays hermetic, offline, and fast. <c>scripts\Test-LanguageServerSemanticRoundTrip.ps1</c> is
/// the lane entry point, wired into <c>area-visual-studio.yml</c> after the extension build.
/// </remarks>
public class LanguageServerPackedConsumerTests
{
    // The spec fixture: a leading using inside @script (the showcase convention), a declared
    // member typed by a framework type, and a stray partial identifier awaiting completion.
    private const string ComponentSource =
        "<template>\n" +
        "    <button @click=\"Increment\">{{ Count.Value }}</button>\n" +
        "</template>\n" +
        "@script {\n" +
        "    using Assimalign.Viu.Reactivity;\n" +
        "\n" +
        "    public Reference<int> Count = Reactive.Reference(0);\n" +
        "\n" +
        "    Inc\n" +
        "}\n";

    private readonly ITestOutputHelper testOutput;

    public LanguageServerPackedConsumerTests(ITestOutputHelper testOutput)
        => this.testOutput = testOutput;

    [Fact]
    public void PublishedServer_RestoredPackedConsumer_AnswersSemanticCompletionAcrossAllReferenceLayers()
    {
        var fixture = LanguageServerPackedConsumerFixture.TryReadFromEnvironment();
        if (fixture is null)
        {
            // Inert without the lane (see the class remarks).
            return;
        }

        var scaffoldRoot = Path.Combine(Path.GetTempPath(), "viu-lsp-packed-consumer");
        var sessionRoot = Path.Combine(scaffoldRoot, Guid.NewGuid().ToString("N"));
        var projectDirectory = Path.Combine(sessionRoot, "project");
        // The package cache is a stable sibling reused across runs: local re-runs skip the
        // WebAssembly pack download while staying isolated from the machine cache, so an
        // Install-Local same-version repack always takes effect.
        var packageCacheDirectory = Path.Combine(scaffoldRoot, "cache");
        try
        {
            ScaffoldConsumerProject(fixture, projectDirectory, packageCacheDirectory);

            var restoreStopwatch = Stopwatch.StartNew();
            RunDotnetRestore(projectDirectory);
            restoreStopwatch.Stop();

            var serverSize = new FileInfo(fixture.ServerExecutable).Length;
            testOutput.WriteLine(
                $"server executable: {fixture.ServerExecutable} ({serverSize:N0} bytes)");
            testOutput.WriteLine($"dotnet restore: {restoreStopwatch.Elapsed.TotalSeconds:N1} s");

            using var client = new LanguageServerProcessClient(fixture.ServerExecutable);
            try
            {
                RunProtocolSession(client, projectDirectory);
            }
            catch (Exception exception) when (exception is not TimeoutException)
            {
                throw new InvalidOperationException(
                    $"The packed-consumer session failed. Server stderr:\n{client.ErrorOutput}",
                    exception);
            }
        }
        finally
        {
            try
            {
                Directory.Delete(sessionRoot, recursive: true);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                // A straggling handle only leaks a temp directory; the assertions already ran.
            }
        }
    }

    private void RunProtocolSession(LanguageServerProcessClient client, string projectDirectory)
    {
        var componentPath = Path.Combine(projectDirectory, "Counter.viu");
        var documentUri = new Uri(componentPath).AbsoluteUri;
        var notifications = new List<JsonDocument>();

        client.Send(
            """
            {"jsonrpc":"2.0","id":"init","method":"initialize","params":{"processId":null,"rootUri":null,"capabilities":{"textDocument":{"completion":{"completionItem":{"documentationFormat":["markdown","plaintext"]}},"documentSymbol":{"hierarchicalDocumentSymbolSupport":true}}}}}
            """);
        client.ReadResponse("init", notifications).Dispose();
        client.Send(
            """
            {"jsonrpc":"2.0","method":"initialized","params":{}}
            """);

        client.Send(CreateOpenMessage(documentUri, ComponentSource));
        client.ReadUntilNotification("textDocument/publishDiagnostics", notifications);

        // Probe B — sibling sources. The caret sits after the stray `Inc` (line 8): `Increment` is
        // declared only in the sibling Counter.Handlers.cs partial, so its presence is impossible
        // from the syntax-only path and proves sibling .cs files are in the compilation.
        var coldStopwatch = Stopwatch.StartNew();
        using (var response = client.ReadResponseAfterSend(
                   CreateCompletionMessage(documentUri, "stray", line: 8, character: 7),
                   "stray",
                   notifications))
        {
            coldStopwatch.Stop();
            var items = response.RootElement.GetProperty("result").GetProperty("items");
            var increment = FindItem(items, "Increment");
            increment.ShouldNotBeNull();
            increment.Value.GetProperty("kind").GetInt32().ShouldBe(2);
            increment.Value.GetProperty("detail").GetString()!.ShouldContain("Increment()");
        }

        testOutput.WriteLine(
            $"cold semantic completion (first request): {coldStopwatch.Elapsed.TotalMilliseconds:N0} ms");

        // The projection-declared member surviving in semantic mode, with its type resolved by the
        // real compilation (the blank member line has no typed prefix, so nothing is filtered).
        using (var response = client.ReadResponseAfterSend(
                   CreateCompletionMessage(documentUri, "members", line: 7, character: 0),
                   "members",
                   notifications))
        {
            var items = response.RootElement.GetProperty("result").GetProperty("items");
            var declared = FindItem(items, "Count");
            declared.ShouldNotBeNull();
            declared.Value.GetProperty("kind").GetInt32().ShouldBe(5);
            declared.Value.GetProperty("detail").GetString()!.ShouldContain("Reference<int>");
            FindItem(items, "Increment").ShouldNotBeNull();
        }

        // Probe A — framework/package resolution. `Value` lives on
        // Assimalign.Viu.Reactivity.Reference<T> inside the Assimalign.Viu.App.Ref targeting pack
        // delivered via frameworkReferences/downloadDependencies: this is the probe that fails if
        // reference resolution reads only targets[].compile.
        var changedSource = ComponentSource.Replace(
            "    Inc\n",
            "        Count.\n",
            StringComparison.Ordinal);
        client.Send(CreateChangeMessage(documentUri, changedSource));
        var warmStopwatch = Stopwatch.StartNew();
        using (var response = client.ReadResponseAfterSend(
                   CreateCompletionMessage(documentUri, "value", line: 8, character: 14),
                   "value",
                   notifications))
        {
            warmStopwatch.Stop();
            var items = response.RootElement.GetProperty("result").GetProperty("items");
            var value = FindItem(items, "Value");
            value.ShouldNotBeNull();
            value.Value.GetProperty("kind").GetInt32().ShouldBe(10);
            value.Value.GetProperty("detail").GetString()!.ShouldContain("int");
        }

        testOutput.WriteLine(
            $"warm semantic completion (after didChange): {warmStopwatch.Elapsed.TotalMilliseconds:N0} ms");

        client.Send(
            """
            {"jsonrpc":"2.0","id":"shutdown","method":"shutdown","params":null}
            """);
        client.ReadResponse("shutdown", notifications).Dispose();
        client.Send(
            """
            {"jsonrpc":"2.0","method":"exit"}
            """);
        client.WaitForExit(TimeSpan.FromSeconds(30)).ShouldBe(0);

        // Health: a restored packed consumer must never report degradation — zero Warnings across
        // the whole session, and at most one Information availability message.
        var logMessages = notifications
            .Where(
                message =>
                    message.RootElement.TryGetProperty("method", out var method) &&
                    method.GetString() == "window/logMessage")
            .Select(message => message.RootElement.GetProperty("params"))
            .ToList();
        logMessages.ShouldAllBe(parameters => parameters.GetProperty("type").GetInt32() == 3);
        logMessages.Count.ShouldBeLessThanOrEqualTo(1);

        foreach (var notification in notifications)
        {
            notification.Dispose();
        }
    }

    private static void ScaffoldConsumerProject(
        LanguageServerPackedConsumerFixture fixture,
        string projectDirectory,
        string packageCacheDirectory)
    {
        Directory.CreateDirectory(projectDirectory);
        Directory.CreateDirectory(packageCacheDirectory);

        // Modeled on the viu-examples consumer configuration: the repo-local feed serves every
        // Assimalign.Viu.* package, nuget.org serves the rest, and the stable cache keeps
        // same-version repacks isolated from the machine-wide NuGet cache.
        File.WriteAllText(
            Path.Combine(projectDirectory, "nuget.config"),
            $"""
             <?xml version="1.0" encoding="utf-8"?>
             <configuration>
                 <packageSources>
                     <clear />
                     <add key="viu-local" value="{fixture.PackageFeed}" />
                     <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                 </packageSources>
                 <packageSourceMapping>
                     <packageSource key="viu-local">
                         <package pattern="Assimalign.Viu.*" />
                     </packageSource>
                     <packageSource key="nuget.org">
                         <package pattern="*" />
                     </packageSource>
                 </packageSourceMapping>
                 <config>
                     <add key="globalPackagesFolder" value="{packageCacheDirectory}" />
                 </config>
             </configuration>
             """);

        // Only the msbuild-sdks pin: pinning sdk.version would re-introduce the muxer-resolution
        // exposure this architecture deliberately avoids.
        File.WriteAllText(
            Path.Combine(projectDirectory, "global.json"),
            JsonSerializer.Serialize(
                new Dictionary<string, object>
                {
                    ["msbuild-sdks"] = new Dictionary<string, string>
                    {
                        ["Assimalign.Viu.Sdk"] = fixture.SdkVersion,
                    },
                }));

        File.WriteAllText(
            Path.Combine(projectDirectory, "App.csproj"),
            """
            <Project Sdk="Assimalign.Viu.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        File.WriteAllText(Path.Combine(projectDirectory, "Counter.viu"), ComponentSource);

        // The sibling partial: namespace and class name match SingleFileComponentNameResolver for
        // a root-level file with the default RootNamespace (the project name).
        File.WriteAllText(
            Path.Combine(projectDirectory, "Counter.Handlers.cs"),
            """
            namespace App;

            public partial class Counter
            {
                public void Increment()
                {
                    Count.Value = Count.Value + 1;
                }
            }
            """);
    }

    private static void RunDotnetRestore(string projectDirectory)
    {
        var startInfo = new ProcessStartInfo("dotnet", "restore")
        {
            WorkingDirectory = projectDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var restore = Process.Start(startInfo)
            ?? throw new InvalidOperationException("dotnet restore failed to start.");
        var standardOutput = restore.StandardOutput.ReadToEndAsync();
        var standardError = restore.StandardError.ReadToEndAsync();
        if (!restore.WaitForExit((int)TimeSpan.FromMinutes(10).TotalMilliseconds))
        {
            restore.Kill(entireProcessTree: true);
            throw new TimeoutException("dotnet restore did not complete within ten minutes.");
        }

        restore.ExitCode.ShouldBe(
            0,
            $"dotnet restore failed.\n{standardOutput.Result}\n{standardError.Result}");
    }

    private static string CreateOpenMessage(string documentUri, string text)
        => JsonSerializer.Serialize(
            new
            {
                jsonrpc = "2.0",
                method = "textDocument/didOpen",
                @params = new
                {
                    textDocument = new
                    {
                        uri = documentUri,
                        languageId = "viu",
                        version = 1,
                        text,
                    },
                },
            });

    private static string CreateChangeMessage(string documentUri, string text)
        => JsonSerializer.Serialize(
            new
            {
                jsonrpc = "2.0",
                method = "textDocument/didChange",
                @params = new
                {
                    textDocument = new
                    {
                        uri = documentUri,
                        version = 2,
                    },
                    contentChanges = new[]
                    {
                        new { text },
                    },
                },
            });

    private static string CreateCompletionMessage(
        string documentUri,
        string identifier,
        int line,
        int character)
        => JsonSerializer.Serialize(
            new
            {
                jsonrpc = "2.0",
                id = identifier,
                method = "textDocument/completion",
                @params = new
                {
                    textDocument = new
                    {
                        uri = documentUri,
                    },
                    position = new
                    {
                        line,
                        character,
                    },
                },
            });

    private static JsonElement? FindItem(JsonElement items, string label)
    {
        foreach (var item in items.EnumerateArray())
        {
            if (item.GetProperty("label").GetString() == label)
            {
                return item;
            }
        }

        return null;
    }
}
