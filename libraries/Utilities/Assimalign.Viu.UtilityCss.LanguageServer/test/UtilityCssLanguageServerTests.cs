using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.UtilityCss.LanguageServer.Tests;

public sealed class UtilityCssLanguageServerTests
{
    [Fact]
    public async Task RunAsync_InitializeHandshake_AdvertisesUtilityCssCapabilitiesAndExitsCleanly()
    {
        var rootUri = new Uri(Path.GetTempPath()).AbsoluteUri;
        var run = await UtilityCssLanguageServerTestProtocol.RunAsync(
            UtilityCssLanguageServerTestProtocol.InitializeRequest("initialize", rootUri),
            UtilityCssLanguageServerTestProtocol.InitializedNotification(),
            UtilityCssLanguageServerTestProtocol.ShutdownRequest("shutdown"),
            UtilityCssLanguageServerTestProtocol.ExitNotification());
        try
        {
            run.ExitCode.ShouldBe(0);
            var initialize = UtilityCssLanguageServerTestProtocol.FindResponse(
                run.Messages,
                "initialize");
            var initializeResult = initialize.GetProperty("result");
            var capabilities = initializeResult.GetProperty("capabilities");

            capabilities.GetProperty("positionEncoding").GetString().ShouldBe("utf-16");
            var synchronization = capabilities.GetProperty("textDocumentSync");
            synchronization.GetProperty("openClose").GetBoolean().ShouldBeTrue();
            synchronization.GetProperty("change").GetInt32().ShouldBe(1);
            capabilities.GetProperty("hoverProvider").GetBoolean().ShouldBeTrue();
            capabilities.GetProperty("colorProvider").GetBoolean().ShouldBeTrue();

            var completionProvider = capabilities.GetProperty("completionProvider");
            completionProvider.GetProperty("resolveProvider").GetBoolean().ShouldBeTrue();
            completionProvider
                .GetProperty("triggerCharacters")
                .EnumerateArray()
                .Select(character => character.GetString())
                .ShouldBe(new[] { "\"", "'", " ", "-", ":" });

            UtilityCssLanguageServerTestProtocol.FindResponse(
                    run.Messages,
                    "shutdown")
                .GetProperty("result")
                .ValueKind.ShouldBe(JsonValueKind.Null);
            var serverVersion = initializeResult
                .GetProperty("serverInfo")
                .GetProperty("version")
                .GetString();
            run.Diagnostics.ShouldContain($"Version: {serverVersion}");
            run.Diagnostics.ShouldContain($"Root: '{rootUri}'");
        }
        finally
        {
            UtilityCssLanguageServerTestProtocol.DisposeMessages(run.Messages);
        }
    }

    [Fact]
    public async Task RunAsync_RequestsForUnsupportedFileType_ReturnEmptyResults()
    {
        const string Candidate = "bg-blue-500";
        const string Source = "<div class=\"bg-blue-500\"></div>\n";
        var candidateStart = Source.IndexOf(Candidate, StringComparison.Ordinal);
        var candidateEnd = candidateStart + Candidate.Length;
        var documentUri = CreateDocumentUri(".txt");

        var run = await UtilityCssLanguageServerTestProtocol.RunAsync(
            UtilityCssLanguageServerTestProtocol.InitializeRequest("initialize"),
            UtilityCssLanguageServerTestProtocol.InitializedNotification(),
            UtilityCssLanguageServerTestProtocol.DidOpenNotification(
                documentUri,
                "plaintext",
                Source),
            UtilityCssLanguageServerTestProtocol.CompletionRequest(
                "completion",
                documentUri,
                Source,
                candidateEnd),
            UtilityCssLanguageServerTestProtocol.HoverRequest(
                "hover",
                documentUri,
                Source,
                candidateStart),
            UtilityCssLanguageServerTestProtocol.DocumentColorRequest(
                "document-color",
                documentUri),
            UtilityCssLanguageServerTestProtocol.ColorPresentationRequest(
                "color-presentation",
                documentUri,
                Source,
                candidateStart,
                candidateEnd,
                0d,
                0d,
                0d,
                1d),
            UtilityCssLanguageServerTestProtocol.ShutdownRequest("shutdown"),
            UtilityCssLanguageServerTestProtocol.ExitNotification());
        try
        {
            var completion = UtilityCssLanguageServerTestProtocol.FindResponse(
                    run.Messages,
                    "completion")
                .GetProperty("result");
            completion.GetProperty("items").GetArrayLength().ShouldBe(0);
            completion.GetProperty("isIncomplete").GetBoolean().ShouldBeFalse();
            UtilityCssLanguageServerTestProtocol.FindResponse(run.Messages, "hover")
                .GetProperty("result")
                .ValueKind.ShouldBe(JsonValueKind.Null);
            UtilityCssLanguageServerTestProtocol.FindResponse(
                    run.Messages,
                    "document-color")
                .GetProperty("result")
                .GetArrayLength()
                .ShouldBe(0);
            UtilityCssLanguageServerTestProtocol.FindResponse(
                    run.Messages,
                    "color-presentation")
                .GetProperty("result")
                .GetArrayLength()
                .ShouldBe(0);
        }
        finally
        {
            UtilityCssLanguageServerTestProtocol.DisposeMessages(run.Messages);
        }
    }

    [Theory]
    [InlineData(".html", "html")]
    [InlineData(".htm", "html")]
    [InlineData(".cshtml", "cshtml")]
    [InlineData(".razor", "razor")]
    [InlineData(".viu", "viu")]
    [InlineData(".vue", "vue")]
    public async Task RunAsync_CompletionInSupportedFileType_ReturnsColorMetadataAndSourceRange(
        string extension,
        string languageIdentifier)
    {
        const string CandidatePrefix = "bg-blue-";
        var isSingleFileComponent = extension is ".viu" or ".vue";
        var source = isSingleFileComponent
            ? "<template>\n    <div class=\"bg-blue-\"></div>\n</template>\n"
            : "<div class=\"bg-blue-\"></div>\n";
        var candidateStart = source.IndexOf(CandidatePrefix, StringComparison.Ordinal);
        var cursor = candidateStart + CandidatePrefix.Length;
        var documentUri = CreateDocumentUri(extension);

        var run = await RunDocumentRequestAsync(
            documentUri,
            languageIdentifier,
            source,
            UtilityCssLanguageServerTestProtocol.CompletionRequest(
                "completion",
                documentUri,
                source,
                cursor));
        try
        {
            var completion = UtilityCssLanguageServerTestProtocol.FindResponse(
                    run.Messages,
                    "completion")
                .GetProperty("result");
            completion.GetProperty("isIncomplete").GetBoolean().ShouldBeFalse();
            var item = UtilityCssLanguageServerTestProtocol.FindCompletionItem(
                completion.GetProperty("items"),
                "bg-blue-500");

            item.GetProperty("data")
                .GetProperty("colorValue")
                .GetString()
                .ShouldBe("oklch(62.3% 0.214 259.815)");
            item.GetProperty("textEdit").GetProperty("newText").GetString()
                .ShouldBe("bg-blue-500");
            AssertRange(
                item.GetProperty("textEdit").GetProperty("range"),
                source,
                candidateStart,
                cursor);
        }
        finally
        {
            UtilityCssLanguageServerTestProtocol.DisposeMessages(run.Messages);
        }
    }

    [Fact]
    public async Task RunAsync_CompletionInViuScriptString_ReturnsNoTemplateCandidates()
    {
        const string ScriptCandidate = "bg-blue-";
        const string Source =
            "<template>\n    <div class=\"flex\"></div>\n</template>\n" +
            "@script {\n" +
            "    const string Markup = \"<div class=\\\"bg-blue-\\\"></div>\";\n" +
            "}\n";
        var scriptStart = Source.IndexOf("@script", StringComparison.Ordinal);
        var candidateStart = Source.IndexOf(
            ScriptCandidate,
            scriptStart,
            StringComparison.Ordinal);
        var cursor = candidateStart + ScriptCandidate.Length;
        var documentUri = CreateDocumentUri(".viu");

        var run = await RunDocumentRequestAsync(
            documentUri,
            "viu",
            Source,
            UtilityCssLanguageServerTestProtocol.CompletionRequest(
                "completion",
                documentUri,
                Source,
                cursor));
        try
        {
            var completion = UtilityCssLanguageServerTestProtocol.FindResponse(
                    run.Messages,
                    "completion")
                .GetProperty("result");
            completion.GetProperty("items").GetArrayLength().ShouldBe(0);
            completion.GetProperty("isIncomplete").GetBoolean().ShouldBeFalse();
        }
        finally
        {
            UtilityCssLanguageServerTestProtocol.DisposeMessages(run.Messages);
        }
    }

    [Fact]
    public async Task RunAsync_HoverOverCandidate_ReturnsGeneratedCssPreviewAsMarkdown()
    {
        const string Candidate = "bg-blue-500";
        const string Source = "<div class=\"bg-blue-500\"></div>\n";
        var candidateStart = Source.IndexOf(Candidate, StringComparison.Ordinal);
        var documentUri = CreateDocumentUri(".html");

        var run = await RunDocumentRequestAsync(
            documentUri,
            "html",
            Source,
            UtilityCssLanguageServerTestProtocol.HoverRequest(
                "hover",
                documentUri,
                Source,
                candidateStart + 3));
        try
        {
            var hover = UtilityCssLanguageServerTestProtocol.FindResponse(
                    run.Messages,
                    "hover")
                .GetProperty("result");
            var contents = hover.GetProperty("contents");
            contents.GetProperty("kind").GetString().ShouldBe("markdown");
            var markdown = contents.GetProperty("value").GetString()
                ?? throw new InvalidDataException("Hover markdown was null.");
            markdown.ShouldContain("```css");
            markdown.ShouldContain(".bg-blue-500");
            markdown.ShouldContain("background-color: var(--color-blue-500);");
            AssertRange(
                hover.GetProperty("range"),
                Source,
                candidateStart,
                candidateStart + Candidate.Length);
        }
        finally
        {
            UtilityCssLanguageServerTestProtocol.DisposeMessages(run.Messages);
        }
    }

    [Fact]
    public async Task RunAsync_DocumentColorAndPresentation_PreserveUtilityCandidateAndRange()
    {
        const string Candidate = "bg-[#123456]";
        const string OpacityCandidate = "bg-blue-500/50";
        const string Source = "<div class=\"bg-[#123456] bg-blue-500/50 block\"></div>\n";
        var candidateStart = Source.IndexOf(Candidate, StringComparison.Ordinal);
        var candidateEnd = candidateStart + Candidate.Length;
        var opacityCandidateStart = Source.IndexOf(
            OpacityCandidate,
            StringComparison.Ordinal);
        var opacityCandidateEnd = opacityCandidateStart + OpacityCandidate.Length;
        var documentUri = CreateDocumentUri(".html");

        var run = await UtilityCssLanguageServerTestProtocol.RunAsync(
            UtilityCssLanguageServerTestProtocol.InitializeRequest("initialize"),
            UtilityCssLanguageServerTestProtocol.InitializedNotification(),
            UtilityCssLanguageServerTestProtocol.DidOpenNotification(
                documentUri,
                "html",
                Source),
            UtilityCssLanguageServerTestProtocol.DocumentColorRequest(
                "document-color",
                documentUri),
            UtilityCssLanguageServerTestProtocol.ColorPresentationRequest(
                "color-presentation",
                documentUri,
                Source,
                candidateStart,
                candidateEnd,
                171d / 255d,
                205d / 255d,
                239d / 255d,
                1d),
            UtilityCssLanguageServerTestProtocol.ShutdownRequest("shutdown"),
            UtilityCssLanguageServerTestProtocol.ExitNotification());
        try
        {
            run.ExitCode.ShouldBe(0);
            var colors = UtilityCssLanguageServerTestProtocol.FindResponse(
                    run.Messages,
                    "document-color")
                .GetProperty("result");
            colors.GetArrayLength().ShouldBe(2);
            var colorInformation = colors[0];
            AssertRange(
                colorInformation.GetProperty("range"),
                Source,
                candidateStart,
                candidateEnd);
            var color = colorInformation.GetProperty("color");
            color.GetProperty("red").GetDouble().ShouldBe(18d / 255d, 0.0000001d);
            color.GetProperty("green").GetDouble().ShouldBe(52d / 255d, 0.0000001d);
            color.GetProperty("blue").GetDouble().ShouldBe(86d / 255d, 0.0000001d);
            color.GetProperty("alpha").GetDouble().ShouldBe(1d);
            var opacityColorInformation = colors[1];
            AssertRange(
                opacityColorInformation.GetProperty("range"),
                Source,
                opacityCandidateStart,
                opacityCandidateEnd);
            opacityColorInformation
                .GetProperty("color")
                .GetProperty("alpha")
                .GetDouble()
                .ShouldBe(0.5d, 0.0000001d);

            var presentations = UtilityCssLanguageServerTestProtocol.FindResponse(
                    run.Messages,
                    "color-presentation")
                .GetProperty("result");
            presentations.GetArrayLength().ShouldBeGreaterThan(0);
            var presentation = presentations[0];
            presentation.GetProperty("label").GetString().ShouldBe("bg-[#abcdef]");
            var edit = presentation.GetProperty("textEdit");
            edit.GetProperty("newText").GetString().ShouldBe("bg-[#abcdef]");
            AssertRange(
                edit.GetProperty("range"),
                Source,
                candidateStart,
                candidateEnd);
        }
        finally
        {
            UtilityCssLanguageServerTestProtocol.DisposeMessages(run.Messages);
        }
    }

    [Fact]
    public async Task RunAsync_CompletionWithManifestTheme_UsesEntryStylesheetConfiguration()
    {
        var projectDirectory = Path.Combine(
            Path.GetTempPath(),
            "viu-utility-css-language-server-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projectDirectory);
        try
        {
            var projectPath = Path.Combine(projectDirectory, "Application.csproj");
            var stylesheetPath = Path.Combine(projectDirectory, "utilities.css");
            var documentPath = Path.Combine(projectDirectory, "Index.html");
            var bundleDirectory = Path.Combine(
                projectDirectory,
                "obj",
                "Debug",
                "net10.0",
                "utilitycss");
            var bundlePath = Path.Combine(bundleDirectory, "Application.utilities.css");
            var manifestPath = Path.Combine(
                bundleDirectory,
                "utilitycss.manifest.v1.json");
            Directory.CreateDirectory(bundleDirectory);
            File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            File.WriteAllText(
                stylesheetPath,
                "@theme { --color-editor-brand: #123456; }\n" +
                "@utility editor-surface { " +
                "background-color: var(--color-editor-brand); }");
            File.WriteAllText(bundlePath, "/* generated utility CSS */");

            var manifest = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["engineVersion"] = "1.0.0.0",
                ["entryStylesheetPath"] = stylesheetPath,
                ["sourceFiles"] = new JsonArray(),
                ["themeContentHash"] = new string('0', 64),
                ["bundle"] = new JsonObject
                {
                    ["path"] = bundlePath,
                    ["name"] = Path.GetFileName(bundlePath),
                },
            }.ToJsonString();
            File.WriteAllText(manifestPath, manifest);

            const string CandidatePrefix = "editor";
            const string Source = "<div class=\"editor\"></div>\n";
            File.WriteAllText(documentPath, Source);
            var cursor = Source.IndexOf(CandidatePrefix, StringComparison.Ordinal) +
                CandidatePrefix.Length;
            var documentUri = new Uri(documentPath).AbsoluteUri;

            var run = await UtilityCssLanguageServerTestProtocol.RunAsync(
                UtilityCssLanguageServerTestProtocol.InitializeRequest("initialize"),
                UtilityCssLanguageServerTestProtocol.InitializedNotification(),
                UtilityCssLanguageServerTestProtocol.DidOpenNotification(
                    documentUri,
                    "html",
                    Source),
                UtilityCssLanguageServerTestProtocol.CompletionRequest(
                    "completion",
                    documentUri,
                    Source,
                    cursor),
                UtilityCssLanguageServerTestProtocol.CompletionResolveRequest(
                    "resolve",
                    documentUri,
                    "editor-surface"),
                UtilityCssLanguageServerTestProtocol.ShutdownRequest("shutdown"),
                UtilityCssLanguageServerTestProtocol.ExitNotification());
            try
            {
                var completion = UtilityCssLanguageServerTestProtocol.FindResponse(
                        run.Messages,
                        "completion")
                    .GetProperty("result");
                var item = UtilityCssLanguageServerTestProtocol.FindCompletionItem(
                    completion.GetProperty("items"),
                    "editor-surface");
                item.GetProperty("data")
                    .GetProperty("colorValue")
                    .GetString()
                    .ShouldBe("#123456");

                var resolved = UtilityCssLanguageServerTestProtocol.FindResponse(
                        run.Messages,
                        "resolve")
                    .GetProperty("result");
                var documentation = resolved
                    .GetProperty("documentation")
                    .GetProperty("value")
                    .GetString()
                    ?? throw new InvalidDataException(
                        "Resolved completion documentation was null.");
                documentation.ShouldContain(".editor-surface");
                documentation.ShouldContain(
                    "background-color: var(--color-editor-brand);");
                run.Diagnostics.ShouldContain("sidecar manifest found");
                run.Diagnostics.ShouldContain(manifestPath);
            }
            finally
            {
                UtilityCssLanguageServerTestProtocol.DisposeMessages(run.Messages);
            }
        }
        finally
        {
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_ProjectWithoutSidecar_ReportsMissingManifestOnce()
    {
        var projectDirectory = Path.Combine(
            Path.GetTempPath(),
            "viu-utility-css-language-server-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projectDirectory);
        try
        {
            var projectPath = Path.Combine(projectDirectory, "Application.csproj");
            var documentPath = Path.Combine(projectDirectory, "Index.html");
            File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

            const string Candidate = "bg-blue-500";
            const string Source = "<div class=\"bg-blue-500\"></div>\n";
            File.WriteAllText(documentPath, Source);
            var candidateStart = Source.IndexOf(Candidate, StringComparison.Ordinal);
            var documentUri = new Uri(documentPath).AbsoluteUri;

            var run = await UtilityCssLanguageServerTestProtocol.RunAsync(
                UtilityCssLanguageServerTestProtocol.InitializeRequest("initialize"),
                UtilityCssLanguageServerTestProtocol.InitializedNotification(),
                UtilityCssLanguageServerTestProtocol.DidOpenNotification(
                    documentUri,
                    "html",
                    Source),
                UtilityCssLanguageServerTestProtocol.CompletionRequest(
                    "completion",
                    documentUri,
                    Source,
                    candidateStart + Candidate.Length),
                UtilityCssLanguageServerTestProtocol.HoverRequest(
                    "hover",
                    documentUri,
                    Source,
                    candidateStart),
                UtilityCssLanguageServerTestProtocol.ShutdownRequest("shutdown"),
                UtilityCssLanguageServerTestProtocol.ExitNotification());
            try
            {
                run.Diagnostics.ShouldContain("sidecar manifest missing");
                run.Diagnostics.ShouldContain(projectDirectory);
                run.Diagnostics
                    .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                    .Count(line => line.Contains(
                        "sidecar manifest missing",
                        StringComparison.Ordinal))
                    .ShouldBe(1);
            }
            finally
            {
                UtilityCssLanguageServerTestProtocol.DisposeMessages(run.Messages);
            }
        }
        finally
        {
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_ProjectWithInvalidSidecar_ReportsInvalidManifest()
    {
        var projectDirectory = Path.Combine(
            Path.GetTempPath(),
            "viu-utility-css-language-server-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projectDirectory);
        try
        {
            var projectPath = Path.Combine(projectDirectory, "Application.csproj");
            var documentPath = Path.Combine(projectDirectory, "Index.html");
            var sidecarDirectory = Path.Combine(
                projectDirectory,
                "obj",
                "Debug",
                "net10.0",
                "utilitycss");
            var manifestPath = Path.Combine(
                sidecarDirectory,
                "utilitycss.manifest.v1.json");
            Directory.CreateDirectory(sidecarDirectory);
            File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            File.WriteAllText(manifestPath, "{");

            const string Candidate = "bg-blue-500";
            const string Source = "<div class=\"bg-blue-500\"></div>\n";
            File.WriteAllText(documentPath, Source);
            var cursor = Source.IndexOf(Candidate, StringComparison.Ordinal) + Candidate.Length;
            var documentUri = new Uri(documentPath).AbsoluteUri;

            var run = await RunDocumentRequestAsync(
                documentUri,
                "html",
                Source,
                UtilityCssLanguageServerTestProtocol.CompletionRequest(
                    "completion",
                    documentUri,
                    Source,
                    cursor));
            try
            {
                run.Diagnostics.ShouldContain("sidecar manifest invalid");
                run.Diagnostics.ShouldContain(manifestPath);
            }
            finally
            {
                UtilityCssLanguageServerTestProtocol.DisposeMessages(run.Messages);
            }
        }
        finally
        {
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_CompletionExceedsEngineBudget_ReturnsIncompleteBoundedList()
    {
        const string Source = "<div class=\"\"></div>\n";
        var cursor = Source.IndexOf("\"\"", StringComparison.Ordinal) + 1;
        var documentUri = CreateDocumentUri(".html");

        var run = await RunDocumentRequestAsync(
            documentUri,
            "html",
            Source,
            UtilityCssLanguageServerTestProtocol.CompletionRequest(
                "completion",
                documentUri,
                Source,
                cursor));
        try
        {
            var completion = UtilityCssLanguageServerTestProtocol.FindResponse(
                    run.Messages,
                    "completion")
                .GetProperty("result");
            completion.GetProperty("items").GetArrayLength().ShouldBe(500);
            completion.GetProperty("isIncomplete").GetBoolean().ShouldBeTrue();
        }
        finally
        {
            UtilityCssLanguageServerTestProtocol.DisposeMessages(run.Messages);
        }
    }

    private static async Task<(
        int ExitCode,
        List<JsonDocument> Messages,
        string Diagnostics)> RunDocumentRequestAsync(
        string documentUri,
        string languageIdentifier,
        string source,
        string request) =>
        await UtilityCssLanguageServerTestProtocol.RunAsync(
            UtilityCssLanguageServerTestProtocol.InitializeRequest("initialize"),
            UtilityCssLanguageServerTestProtocol.InitializedNotification(),
            UtilityCssLanguageServerTestProtocol.DidOpenNotification(
                documentUri,
                languageIdentifier,
                source),
            request,
            UtilityCssLanguageServerTestProtocol.ShutdownRequest("shutdown"),
            UtilityCssLanguageServerTestProtocol.ExitNotification());

    private static string CreateDocumentUri(string extension)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "viu-utility-css-language-server-tests",
            Guid.NewGuid().ToString("N"),
            "Document" + extension);
        return new Uri(path).AbsoluteUri;
    }

    private static void AssertRange(
        JsonElement range,
        string source,
        int start,
        int end)
    {
        var expectedStart = UtilityCssLanguageServerTestProtocol.GetPosition(source, start);
        var expectedEnd = UtilityCssLanguageServerTestProtocol.GetPosition(source, end);
        var actualStart = range.GetProperty("start");
        var actualEnd = range.GetProperty("end");

        actualStart.GetProperty("line").GetInt32().ShouldBe(expectedStart.Line);
        actualStart.GetProperty("character").GetInt32().ShouldBe(expectedStart.Character);
        actualEnd.GetProperty("line").GetInt32().ShouldBe(expectedEnd.Line);
        actualEnd.GetProperty("character").GetInt32().ShouldBe(expectedEnd.Character);
    }
}
