using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Tooling.LanguageServer;

/// <summary>
/// Pins the degradation protocol ([V01.01.12.23], #259): missing project state serves the
/// syntax-only answer with exactly one Warning, an unrestored reference degrades with a detail
/// naming what was skipped, a loose file is silent, the degraded path is byte-identical to the
/// pre-semantic service (the zero-prerequisite guarantee), and status reporting is keyed by
/// per-project state transitions — recovery emits its Information message and a later
/// re-degradation re-warns.
/// </summary>
public class LanguageServerSemanticDegradationTests
{
    [Fact]
    public async Task RunAsync_OwnedProjectWithoutState_ServesSyntaxOnlyAndWarnsOnce()
    {
        var directory = SemanticDegradationFixture.CreateFixtureRoot("missing-state");
        try
        {
            var componentPath = SemanticDegradationFixture.CreateProjectWithoutState(directory);
            var documentUri = new Uri(componentPath).AbsoluteUri;
            var changedText = SemanticDegradationFixture.ComponentText.Replace(
                "    \n",
                "    C\n",
                StringComparison.Ordinal);

            await using var input = new MemoryStream(
                Encoding.UTF8.GetBytes(
                    Frame(CreateOpenMessage(documentUri, SemanticDegradationFixture.ComponentText)) +
                    Frame(CreateCompletionMessage(documentUri, "first", line: 2, character: 4)) +
                    Frame(CreateChangeMessage(documentUri, changedText)) +
                    Frame(CreateCompletionMessage(documentUri, "second", line: 2, character: 5)) +
                    Frame(CreateCompletionMessage(documentUri, "third", line: 2, character: 5)) +
                    Frame(
                        """
                        {"jsonrpc":"2.0","id":9,"method":"shutdown","params":null}
                        """) +
                    Frame(
                        """
                        {"jsonrpc":"2.0","method":"exit"}
                        """)));
            await using var output = new MemoryStream();
            var host = new LanguageServerHost();

            var exitCode = await host.RunAsync(input, output);

            exitCode.ShouldBe(0);
            output.Position = 0;
            var messages = await ReadAllMessagesAsync(output);
            // The not-per-keystroke pin: one open plus three requests observe the same missing
            // state, and the whole stream carries exactly one status notification.
            var logMessages = FindLogMessages(messages);
            logMessages.Count.ShouldBe(1);
            logMessages[0].GetProperty("type").GetInt32().ShouldBe(2);
            var warning = logMessages[0].GetProperty("message").GetString()!;
            warning.ShouldContain("unavailable for App");
            warning.ShouldContain("obj\\project.assets.json");
            warning.ShouldContain("dotnet build");

            // The syntax-only answer survives untouched: the declared member keeps its label,
            // kind, declared-type detail, sort band, and declaration documentation.
            foreach (var identifier in new[] { "first", "second", "third" })
            {
                var items = FindResponse(messages, identifier)
                    .GetProperty("result")
                    .GetProperty("items");
                var declared = FindItem(items, "Count");
                declared.ShouldNotBeNull();
                declared.Value.GetProperty("kind").GetInt32().ShouldBe(5);
                declared.Value.GetProperty("detail").GetString().ShouldBe("Reference<int>");
                declared.Value.GetProperty("sortText").GetString().ShouldBe("00:Count");
                declared.Value.GetProperty("documentation").GetProperty("value").GetString()!
                    .ShouldContain("Declared in this file's @script block");
            }

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
    public async Task RunAsync_OwnedProjectWithoutState_AnswersByteIdenticalToUnownedDocument()
    {
        var directory = SemanticDegradationFixture.CreateFixtureRoot("zero-prerequisite");
        try
        {
            var componentPath = SemanticDegradationFixture.CreateProjectWithoutState(directory);
            var ownedUri = new Uri(componentPath).AbsoluteUri;
            const string unownedUri = "file:///Counter.viu";

            var unownedResult = await RunSingleCompletionSessionAsync(unownedUri);
            var ownedResult = await RunSingleCompletionSessionAsync(ownedUri);

            // The zero-prerequisite guarantee in its strongest form: with no restored project
            // state, the owned document's completion payload is deep-equal to today's syntax-only
            // output — not one field added, removed, reordered, or reshaped — once the only
            // intentionally differing field (the resolve round-trip URI) is normalized.
            NormalizeCompletionResult(ownedResult).ShouldBe(NormalizeCompletionResult(unownedResult));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_ProjectStateWithUnrestoredPackage_ServesSyntaxOnlyAndWarnsWithDetail()
    {
        var directory = SemanticDegradationFixture.CreateFixtureRoot("unrestored-package");
        try
        {
            var componentPath = SemanticDegradationFixture.CreateProjectWithoutState(directory);
            var libraryKey = SemanticDegradationFixture.WriteUnrestoredPackageProjectState(directory);
            var documentUri = new Uri(componentPath).AbsoluteUri;

            await using var input = new MemoryStream(
                Encoding.UTF8.GetBytes(
                    Frame(CreateOpenMessage(documentUri, SemanticDegradationFixture.ComponentText)) +
                    Frame(CreateCompletionMessage(documentUri, "completion", line: 2, character: 4)) +
                    Frame(
                        """
                        {"jsonrpc":"2.0","id":9,"method":"shutdown","params":null}
                        """) +
                    Frame(
                        """
                        {"jsonrpc":"2.0","method":"exit"}
                        """)));
            await using var output = new MemoryStream();
            var host = new LanguageServerHost();

            var exitCode = await host.RunAsync(input, output);

            exitCode.ShouldBe(0);
            output.Position = 0;
            var messages = await ReadAllMessagesAsync(output);
            // The partial-state ladder rung: the answer still arrives (the syntax-only declared
            // member at minimum) and exactly one Warning names the project, what was skipped, and
            // the remedy.
            var items = FindResponse(messages, "completion")
                .GetProperty("result")
                .GetProperty("items");
            var declared = FindItem(items, "Count");
            declared.ShouldNotBeNull();
            declared.Value.GetProperty("detail").GetString().ShouldBe("Reference<int>");

            var logMessages = FindLogMessages(messages);
            logMessages.Count.ShouldBe(1);
            logMessages[0].GetProperty("type").GetInt32().ShouldBe(2);
            var warning = logMessages[0].GetProperty("message").GetString()!;
            warning.ShouldContain("unavailable for App");
            warning.ShouldContain(libraryKey);
            warning.ShouldContain("run dotnet restore");

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
    public async Task RunAsync_LooseDocumentWithoutOwningProject_EmitsNoStatus()
    {
        const string documentUri = "file:///Card.viu";
        await using var input = new MemoryStream(
            Encoding.UTF8.GetBytes(
                Frame(CreateOpenMessage(documentUri, SemanticDegradationFixture.ComponentText)) +
                Frame(CreateCompletionMessage(documentUri, "first", line: 2, character: 4)) +
                Frame(CreateHoverMessage(documentUri, "hover", line: 1, character: 30)) +
                Frame(CreateCompletionMessage(documentUri, "second", line: 2, character: 4)) +
                Frame(
                    """
                    {"jsonrpc":"2.0","id":9,"method":"shutdown","params":null}
                    """) +
                Frame(
                    """
                    {"jsonrpc":"2.0","method":"exit"}
                    """)));
        await using var output = new MemoryStream();
        var host = new LanguageServerHost();

        var exitCode = await host.RunAsync(input, output);

        exitCode.ShouldBe(0);
        output.Position = 0;
        var messages = await ReadAllMessagesAsync(output);
        // A scratch .viu outside any project is baseline behavior, not degradation: several
        // semantic-feature requests produce zero status noise. This is also the standing guard
        // that keeps every unowned-URI protocol test class quiet.
        FindLogMessages(messages).ShouldBeEmpty();
        FindResponse(messages, "first").TryGetProperty("result", out _).ShouldBeTrue();
        FindResponse(messages, "second").TryGetProperty("result", out _).ShouldBeTrue();

        foreach (var message in messages)
        {
            message.Dispose();
        }
    }

    [Fact]
    public async Task RunAsync_ProjectStateTransitions_ReportEachTransitionOnce()
    {
        var directory = SemanticDegradationFixture.CreateFixtureRoot("transitions");
        try
        {
            var componentPath = SemanticDegradationFixture.CreateProjectWithoutState(directory);
            var documentUri = new Uri(componentPath).AbsoluteUri;
            await using var session = new LanguageServerHostSession(new LanguageServerHost());

            await session.SendAsync(CreateOpenMessage(documentUri, SemanticDegradationFixture.ComponentText));
            await session.SendAsync(CreateCompletionMessage(documentUri, "first", line: 2, character: 4));
            using (var first = await session.ReadResponseAsync("first"))
            {
                first.RootElement.TryGetProperty("result", out _).ShouldBeTrue();
            }

            // Recovery happens lazily on the next request: writing the state mid-session flips the
            // project to available, and the transition emits exactly one Information message.
            SemanticDegradationFixture.WriteResolvableProjectState(directory);
            await session.SendAsync(CreateCompletionMessage(documentUri, "second", line: 2, character: 4));
            using (var second = await session.ReadResponseAsync("second"))
            {
                second.RootElement.TryGetProperty("result", out _).ShouldBeTrue();
            }

            // Deduplication is keyed by state-signature transition, not "once ever": deleting the
            // state re-degrades the project and the Warning is allowed to fire again.
            SemanticDegradationFixture.DeleteProjectState(directory);
            await session.SendAsync(CreateCompletionMessage(documentUri, "third", line: 2, character: 4));
            using (var third = await session.ReadResponseAsync("third"))
            {
                third.RootElement.TryGetProperty("result", out _).ShouldBeTrue();
            }

            await session.SendAsync(
                """
                {"jsonrpc":"2.0","id":"shutdown","method":"shutdown","params":null}
                """);
            (await session.ReadResponseAsync("shutdown")).Dispose();
            await session.SendAsync(
                """
                {"jsonrpc":"2.0","method":"exit"}
                """);
            var exitCode = await session.CompleteAsync();

            exitCode.ShouldBe(0);
            var logMessages = session.Notifications
                .Where(
                    message =>
                        message.RootElement.TryGetProperty("method", out var method) &&
                        method.GetString() == "window/logMessage")
                .Select(message => message.RootElement.GetProperty("params"))
                .ToList();
            logMessages.Count.ShouldBe(3);
            logMessages[0].GetProperty("type").GetInt32().ShouldBe(2);
            logMessages[0].GetProperty("message").GetString()!
                .ShouldContain("obj\\project.assets.json");
            logMessages[1].GetProperty("type").GetInt32().ShouldBe(3);
            logMessages[1].GetProperty("message").GetString().ShouldBe(
                $"Viu semantic IntelliSense active for App ('{Path.Combine(directory, "App.csproj")}').");
            logMessages[2].GetProperty("type").GetInt32().ShouldBe(2);
            logMessages[2].GetProperty("message").GetString().ShouldBe(
                logMessages[0].GetProperty("message").GetString());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<string> RunSingleCompletionSessionAsync(string documentUri)
    {
        await using var input = new MemoryStream(
            Encoding.UTF8.GetBytes(
                Frame(CreateOpenMessage(documentUri, SemanticDegradationFixture.ComponentText)) +
                Frame(CreateCompletionMessage(documentUri, "completion", line: 2, character: 4)) +
                Frame(
                    """
                    {"jsonrpc":"2.0","id":9,"method":"shutdown","params":null}
                    """) +
                Frame(
                    """
                    {"jsonrpc":"2.0","method":"exit"}
                    """)));
        await using var output = new MemoryStream();
        var host = new LanguageServerHost();

        var exitCode = await host.RunAsync(input, output);

        exitCode.ShouldBe(0);
        output.Position = 0;
        var messages = await ReadAllMessagesAsync(output);
        var result = FindResponse(messages, "completion").GetProperty("result").GetRawText();
        foreach (var message in messages)
        {
            message.Dispose();
        }

        return result;
    }

    private static string NormalizeCompletionResult(string resultJson)
    {
        var result = JsonNode.Parse(resultJson)!;
        foreach (var item in result["items"]!.AsArray())
        {
            if (item?["data"] is JsonObject data && data.ContainsKey("documentUri"))
            {
                data["documentUri"] = "normalized";
            }
        }

        return result.ToJsonString();
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
        => CreatePositionRequest(documentUri, identifier, "textDocument/completion", line, character);

    private static string CreateHoverMessage(
        string documentUri,
        string identifier,
        int line,
        int character)
        => CreatePositionRequest(documentUri, identifier, "textDocument/hover", line, character);

    private static string CreatePositionRequest(
        string documentUri,
        string identifier,
        string method,
        int line,
        int character)
        => JsonSerializer.Serialize(
            new
            {
                jsonrpc = "2.0",
                id = identifier,
                method,
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

    private static List<JsonElement> FindLogMessages(List<JsonDocument> messages)
        => messages
            .Where(
                message =>
                    message.RootElement.TryGetProperty("method", out var method) &&
                    method.GetString() == "window/logMessage")
            .Select(message => message.RootElement.GetProperty("params"))
            .ToList();

    private static JsonElement FindResponse(List<JsonDocument> messages, string identifier)
        => messages.Single(
                message =>
                    message.RootElement.TryGetProperty("id", out var responseIdentifier) &&
                    responseIdentifier.ValueKind == JsonValueKind.String &&
                    responseIdentifier.GetString() == identifier)
            .RootElement;

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
}
