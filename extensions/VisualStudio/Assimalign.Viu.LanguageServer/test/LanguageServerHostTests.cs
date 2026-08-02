using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Shouldly;

using Xunit;

using Assimalign.Viu.LanguageService;

namespace Assimalign.Viu.LanguageServer;

public class LanguageServerHostTests
{
    [Fact]
    public async Task RunAsync_EditorSession_InitializesPublishesDiagnosticsAndCompletes()
    {
        var inputBytes = Encoding.UTF8.GetBytes(
            Frame(
                """
                {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"capabilities":{}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///Counter.viu","languageId":"viu","version":1,"text":"@script\n"}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","id":2,"method":"textDocument/completion","params":{"textDocument":{"uri":"file:///Counter.viu"},"position":{"line":0,"character":7}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","id":3,"method":"shutdown","params":null}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","method":"exit"}
                """));

        await using var input = new MemoryStream(inputBytes);
        await using var output = new MemoryStream();
        var host = new LanguageServerHost();

        var exitCode = await host.RunAsync(input, output);

        exitCode.ShouldBe(0);
        output.Position = 0;
        var messages = await ReadAllMessagesAsync(output);
        messages.Count.ShouldBe(4);

        var initialize = messages[0].RootElement;
        initialize.GetProperty("id").GetInt32().ShouldBe(1);
        var triggerCharacters = initialize
            .GetProperty("result")
            .GetProperty("capabilities")
            .GetProperty("completionProvider")
            .GetProperty("triggerCharacters")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();
        triggerCharacters.ShouldContain(" ");
        triggerCharacters.ShouldContain("-");
        triggerCharacters.ShouldContain("[");
        triggerCharacters.ShouldContain("(");
        triggerCharacters.ShouldContain(":");
        triggerCharacters.ShouldContain("/");
        triggerCharacters.ShouldContain("!");

        var diagnostics = messages[1].RootElement;
        diagnostics.GetProperty("method").GetString().ShouldBe("textDocument/publishDiagnostics");
        diagnostics
            .GetProperty("params")
            .GetProperty("diagnostics")[0]
            .GetProperty("code")
            .GetString()
            .ShouldBe("VIU1003");

        var completion = messages[2].RootElement;
        completion.GetProperty("id").GetInt32().ShouldBe(2);
        completion
            .GetProperty("result")
            .GetProperty("items")
            .GetArrayLength()
            .ShouldBeGreaterThan(0);

        messages[3].RootElement.GetProperty("id").GetInt32().ShouldBe(3);

        foreach (var message in messages)
        {
            message.Dispose();
        }
    }

    [Fact]
    public async Task RunAsync_CompletionWithEditRange_SerializesFilterAndTextEdit()
    {
        var inputBytes = Encoding.UTF8.GetBytes(
            Frame(
                """
                {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///Counter.viu","languageId":"viu","version":1,"text":"<template>\n    <div class=\"hover:bg-red\"></div>\n</template>\n"}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","id":"completion","method":"textDocument/completion","params":{"textDocument":{"uri":"file:///Counter.viu"},"position":{"line":1,"character":23}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","method":"exit"}
                """));

        await using var input = new MemoryStream(inputBytes);
        await using var output = new MemoryStream();
        var host = new LanguageServerHost(new CompletionLanguageService());

        await host.RunAsync(input, output);

        output.Position = 0;
        var messages = await ReadAllMessagesAsync(output);
        messages.Count.ShouldBe(2);

        var item = messages[1].RootElement
            .GetProperty("result")
            .GetProperty("items")[0];
        item.GetProperty("filterText").GetString().ShouldBe("hover:bg-red");
        var edit = item.GetProperty("textEdit");
        edit.GetProperty("newText").GetString().ShouldBe("hover:bg-red-500");
        edit.GetProperty("range").GetProperty("start").GetProperty("line").GetInt32().ShouldBe(1);
        edit.GetProperty("range").GetProperty("start").GetProperty("character").GetInt32().ShouldBe(8);
        edit.GetProperty("range").GetProperty("end").GetProperty("character").GetInt32().ShouldBe(20);

        foreach (var message in messages)
        {
            message.Dispose();
        }
    }

    [Fact]
    public async Task RunAsync_VueDocumentOwnedByNonViuProject_DoesNotReachLanguageService()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "viu-language-server-mixed-solution-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
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
            const string source = "<template><div class=\"bg-red-500\"></div></template>";
            File.WriteAllText(componentPath, source);
            var documentUri = new Uri(componentPath).AbsoluteUri;

            var openMessage = JsonSerializer.Serialize(
                new
                {
                    jsonrpc = "2.0",
                    method = "textDocument/didOpen",
                    @params = new
                    {
                        textDocument = new
                        {
                            uri = documentUri,
                            languageId = "vue",
                            version = 1,
                            text = source,
                        },
                    },
                });
            var changeMessage = JsonSerializer.Serialize(
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
                            new
                            {
                                text = source,
                            },
                        },
                    },
                });
            var completionMessage = JsonSerializer.Serialize(
                new
                {
                    jsonrpc = "2.0",
                    id = "completion",
                    method = "textDocument/completion",
                    @params = new
                    {
                        textDocument = new
                        {
                            uri = documentUri,
                        },
                        position = new
                        {
                            line = 0,
                            character = 20,
                        },
                    },
                });
            var hoverMessage = JsonSerializer.Serialize(
                new
                {
                    jsonrpc = "2.0",
                    id = "hover",
                    method = "textDocument/hover",
                    @params = new
                    {
                        textDocument = new
                        {
                            uri = documentUri,
                        },
                        position = new
                        {
                            line = 0,
                            character = 20,
                        },
                    },
                });
            var documentSymbolMessage = JsonSerializer.Serialize(
                new
                {
                    jsonrpc = "2.0",
                    id = "symbols",
                    method = "textDocument/documentSymbol",
                    @params = new
                    {
                        textDocument = new
                        {
                            uri = documentUri,
                        },
                    },
                });
            var foldingRangeMessage = JsonSerializer.Serialize(
                new
                {
                    jsonrpc = "2.0",
                    id = "folding",
                    method = "textDocument/foldingRange",
                    @params = new
                    {
                        textDocument = new
                        {
                            uri = documentUri,
                        },
                    },
                });
            var codeActionMessage = JsonSerializer.Serialize(
                new
                {
                    jsonrpc = "2.0",
                    id = "actions",
                    method = "textDocument/codeAction",
                    @params = new
                    {
                        textDocument = new
                        {
                            uri = documentUri,
                        },
                        range = new
                        {
                            start = new { line = 0, character = 0 },
                            end = new { line = 0, character = 20 },
                        },
                        context = new { diagnostics = Array.Empty<object>() },
                    },
                });
            var resolveMessage = JsonSerializer.Serialize(
                new
                {
                    jsonrpc = "2.0",
                    id = "resolve",
                    method = "completionItem/resolve",
                    @params = new
                    {
                        label = "bg-red-500",
                        data = new
                        {
                            documentUri,
                            label = "bg-red-500",
                        },
                    },
                });
            var closeMessage = JsonSerializer.Serialize(
                new
                {
                    jsonrpc = "2.0",
                    method = "textDocument/didClose",
                    @params = new
                    {
                        textDocument = new
                        {
                            uri = documentUri,
                        },
                    },
                });
            var inputBytes = Encoding.UTF8.GetBytes(
                Frame(
                    """
                    {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"capabilities":{"textDocument":{"documentSymbol":{"hierarchicalDocumentSymbolSupport":true},"codeAction":{"codeActionLiteralSupport":{"codeActionKind":{"valueSet":["quickfix"]}}}}}}}
                    """) +
                Frame(openMessage) +
                Frame(changeMessage) +
                Frame(completionMessage) +
                Frame(hoverMessage) +
                Frame(documentSymbolMessage) +
                Frame(foldingRangeMessage) +
                Frame(codeActionMessage) +
                Frame(resolveMessage) +
                Frame(closeMessage) +
                Frame(
                    """
                    {"jsonrpc":"2.0","method":"exit"}
                    """));

            await using var input = new MemoryStream(inputBytes);
            await using var output = new MemoryStream();
            var languageService = new RecordingLanguageService();
            var host = new LanguageServerHost(languageService);

            await host.RunAsync(input, output);

            languageService.OpenCount.ShouldBe(0);
            languageService.ChangeCount.ShouldBe(0);
            languageService.CloseCount.ShouldBe(0);
            languageService.DiagnosticsCount.ShouldBe(0);
            languageService.CompletionCount.ShouldBe(0);
            languageService.HoverCount.ShouldBe(0);
            languageService.ResolveCount.ShouldBe(0);
            languageService.DocumentSymbolCount.ShouldBe(0);
            languageService.FoldingRangeCount.ShouldBe(0);
            languageService.CodeActionCount.ShouldBe(0);

            output.Position = 0;
            var messages = await ReadAllMessagesAsync(output);
            messages.Count.ShouldBe(10);
            messages[3].RootElement
                .GetProperty("result")
                .GetProperty("items")
                .GetArrayLength()
                .ShouldBe(0);
            messages[4].RootElement
                .GetProperty("result")
                .ValueKind
                .ShouldBe(JsonValueKind.Null);
            messages[5].RootElement.GetProperty("result").GetArrayLength().ShouldBe(0);
            messages[6].RootElement.GetProperty("result").GetArrayLength().ShouldBe(0);
            messages[7].RootElement.GetProperty("result").GetArrayLength().ShouldBe(0);
            // The resolve echoes the unsupported document's item back unchanged.
            messages[8].RootElement
                .GetProperty("result")
                .GetProperty("label")
                .GetString()
                .ShouldBe("bg-red-500");

            foreach (var message in messages)
            {
                message.Dispose();
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_ProjectUtilityEntry_FeedsThemeAwareCompletion()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "viu-language-server-project-theme-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(
                Path.Combine(directory, "Application.csproj"),
                """
                <Project Sdk="Assimalign.Viu.Sdk">
                  <ItemGroup>
                    <ViuUtilityCss Include="Utilities.css" />
                  </ItemGroup>
                </Project>
                """);
            File.WriteAllText(
                Path.Combine(directory, "Utilities.css"),
                "@theme { --color-brand: #123456; }");
            const string templateLine =
                "  <div class=\"bg-bra\"></div>";
            var source =
                $"<template>\n{templateLine}\n</template>\n";
            var componentPath = Path.Combine(directory, "Card.vue");
            File.WriteAllText(componentPath, source);
            var documentUri = new Uri(componentPath).AbsoluteUri;
            var character = templateLine.IndexOf(
                    "bg-bra",
                    StringComparison.Ordinal) +
                "bg-bra".Length;
            var openMessage = JsonSerializer.Serialize(
                new
                {
                    jsonrpc = "2.0",
                    method = "textDocument/didOpen",
                    @params = new
                    {
                        textDocument = new
                        {
                            uri = documentUri,
                            languageId = "vue",
                            version = 1,
                            text = source,
                        },
                    },
                });
            var completionMessage = JsonSerializer.Serialize(
                new
                {
                    jsonrpc = "2.0",
                    id = "completion",
                    method = "textDocument/completion",
                    @params = new
                    {
                        textDocument = new
                        {
                            uri = documentUri,
                        },
                        position = new
                        {
                            line = 1,
                            character,
                        },
                    },
                });
            // The resolve request round-trips exactly the data payload the completion item
            // carries; the item's data is asserted below to match this payload.
            var resolveMessage = JsonSerializer.Serialize(
                new
                {
                    jsonrpc = "2.0",
                    id = "resolve",
                    method = "completionItem/resolve",
                    @params = new
                    {
                        label = "bg-brand",
                        data = new
                        {
                            documentUri,
                            label = "bg-brand",
                        },
                    },
                });
            var inputBytes = Encoding.UTF8.GetBytes(
                Frame(openMessage) +
                Frame(completionMessage) +
                Frame(resolveMessage) +
                Frame(
                    """
                    {"jsonrpc":"2.0","method":"exit"}
                    """));

            await using var input = new MemoryStream(inputBytes);
            await using var output = new MemoryStream();
            var host = new LanguageServerHost();

            await host.RunAsync(input, output);

            output.Position = 0;
            var messages = await ReadAllMessagesAsync(output);
            messages.Count.ShouldBe(3);
            var items = messages[1].RootElement
                .GetProperty("result")
                .GetProperty("items")
                .EnumerateArray()
                .ToArray();
            var brand = items.Single(
                item =>
                    item.GetProperty("label").GetString() ==
                    "bg-brand");
            brand.TryGetProperty("documentation", out _).ShouldBeFalse();
            var data = brand.GetProperty("data");
            data.GetProperty("documentUri").GetString().ShouldBe(documentUri);
            data.GetProperty("label").GetString().ShouldBe("bg-brand");

            var resolved = messages[2].RootElement.GetProperty("result");
            var documentation = resolved.GetProperty("documentation")
                .GetProperty("value")
                .GetString();
            documentation.ShouldNotBeNull();
            documentation!.ShouldContain("var(--color-brand)");

            foreach (var message in messages)
            {
                message.Dispose();
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<List<JsonDocument>> ReadAllMessagesAsync(Stream stream)
    {
        var reader = new LanguageServerProtocolMessageReader(stream);
        var messages = new List<JsonDocument>();
        while (true)
        {
            var message = await reader.ReadAsync();
            if (message is null)
            {
                return messages;
            }

            messages.Add(message);
        }
    }

    private static string Frame(string payload)
        => $"Content-Length: {Encoding.UTF8.GetByteCount(payload)}\r\n\r\n{payload}";

    private sealed class CompletionLanguageService : IViuLanguageService
    {
        public void OpenDocument(string documentUri, string text, int? version)
        {
        }

        public bool ChangeDocument(
            string documentUri,
            int? version,
            IReadOnlyList<LanguageDocumentChange> changes)
            => true;

        public bool CloseDocument(string documentUri) => true;

        public IReadOnlyList<LanguageDiagnostic> GetDiagnostics(string documentUri)
            => Array.Empty<LanguageDiagnostic>();

        public IReadOnlyList<LanguageCompletionItem> GetCompletions(
            string documentUri,
            LanguagePosition position)
            =>
            [
                new LanguageCompletionItem(
                    "hover:bg-red-500",
                    LanguageCompletionItemKind.Property,
                    "Viu utility",
                    "Sets the background color on hover.",
                    "hover:bg-red-500",
                    IsSnippet: false,
                    SortText: "01",
                    EditRange: new LanguageRange(
                        new LanguagePosition(1, 8),
                        new LanguagePosition(1, 20)),
                    FilterText: "hover:bg-red"),
            ];

        public LanguageHover? GetHover(string documentUri, LanguagePosition position) => null;

        public string? ResolveCompletionDocumentation(
            string documentUri,
            string completionLabel)
            => null;

        public IReadOnlyList<LanguageDocumentSymbol> GetDocumentSymbols(string documentUri)
            => Array.Empty<LanguageDocumentSymbol>();

        public IReadOnlyList<LanguageFoldingRange> GetFoldingRanges(string documentUri)
            => Array.Empty<LanguageFoldingRange>();

        public IReadOnlyList<LanguageCodeAction> GetCodeActions(
            string documentUri,
            LanguageRange range)
            => Array.Empty<LanguageCodeAction>();
    }

    private sealed class RecordingLanguageService : IViuLanguageService
    {
        internal int OpenCount { get; private set; }

        internal int ChangeCount { get; private set; }

        internal int CloseCount { get; private set; }

        internal int DiagnosticsCount { get; private set; }

        internal int CompletionCount { get; private set; }

        internal int HoverCount { get; private set; }

        internal int ResolveCount { get; private set; }

        internal int DocumentSymbolCount { get; private set; }

        internal int FoldingRangeCount { get; private set; }

        internal int CodeActionCount { get; private set; }

        public void OpenDocument(string documentUri, string text, int? version)
            => OpenCount++;

        public bool ChangeDocument(
            string documentUri,
            int? version,
            IReadOnlyList<LanguageDocumentChange> changes)
        {
            ChangeCount++;
            return true;
        }

        public bool CloseDocument(string documentUri)
        {
            CloseCount++;
            return true;
        }

        public IReadOnlyList<LanguageDiagnostic> GetDiagnostics(string documentUri)
        {
            DiagnosticsCount++;
            return Array.Empty<LanguageDiagnostic>();
        }

        public IReadOnlyList<LanguageCompletionItem> GetCompletions(
            string documentUri,
            LanguagePosition position)
        {
            CompletionCount++;
            return Array.Empty<LanguageCompletionItem>();
        }

        public LanguageHover? GetHover(
            string documentUri,
            LanguagePosition position)
        {
            HoverCount++;
            return null;
        }

        public string? ResolveCompletionDocumentation(
            string documentUri,
            string completionLabel)
        {
            ResolveCount++;
            return null;
        }

        public IReadOnlyList<LanguageDocumentSymbol> GetDocumentSymbols(string documentUri)
        {
            DocumentSymbolCount++;
            return Array.Empty<LanguageDocumentSymbol>();
        }

        public IReadOnlyList<LanguageFoldingRange> GetFoldingRanges(string documentUri)
        {
            FoldingRangeCount++;
            return Array.Empty<LanguageFoldingRange>();
        }

        public IReadOnlyList<LanguageCodeAction> GetCodeActions(
            string documentUri,
            LanguageRange range)
        {
            CodeActionCount++;
            return Array.Empty<LanguageCodeAction>();
        }
    }
}
