using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageServer.Tests;

/// <summary>
/// The lane-driven real-process proof that a build-emitted class catalog reaches Viu completion
/// over standard input and output ([V01.01.12.30], #346).
/// </summary>
public class LanguageServerClassCatalogProcessTests
{
    [Fact]
    public void BuiltCatalog_ViuDocument_OffersColorCompletionOverStandardInputAndOutput()
    {
        var fixture = Fixture.TryReadFromEnvironment();
        if (fixture is null)
        {
            // Inert outside the package verification lane.
            return;
        }

        var diskSource = File.ReadAllText(fixture.DocumentPath);
        var source = diskSource.Replace(
            fixture.ClassName,
            fixture.CompletionPrefix,
            StringComparison.Ordinal);
        source.ShouldNotBe(diskSource, "the fixture class must occur in the Viu document");
        var prefixOffset = source.IndexOf(
            fixture.CompletionPrefix,
            StringComparison.Ordinal);
        prefixOffset.ShouldBeGreaterThanOrEqualTo(0);
        var position = GetPosition(
            source,
            prefixOffset + fixture.CompletionPrefix.Length);
        var documentUri = new Uri(fixture.DocumentPath).AbsoluteUri;
        var notifications = new List<JsonDocument>();

        using var client = new LanguageServerProcessClient(fixture.ServerExecutable);
        try
        {
            client.Send(
                """
                {"jsonrpc":"2.0","id":"init","method":"initialize","params":{"processId":null,"rootUri":null,"capabilities":{"textDocument":{"completion":{"completionItem":{"documentationFormat":["markdown","plaintext"]}}}}}}
                """);
            client.ReadResponse("init", notifications).Dispose();
            client.Send(
                """
                {"jsonrpc":"2.0","method":"initialized","params":{}}
                """);
            client.Send(CreateOpenMessage(documentUri, source));
            client.ReadUntilNotification("textDocument/publishDiagnostics", notifications);

            using (var response = client.ReadResponseAfterSend(
                       CreateCompletionMessage(documentUri, position),
                       "completion",
                       notifications))
            {
                var result = response.RootElement.GetProperty("result");
                var completion = result
                    .GetProperty("items")
                    .EnumerateArray()
                    .Single(item => item.GetProperty("label").GetString() == fixture.ClassName);
                completion.GetProperty("kind").GetInt32().ShouldBe(16);
                completion.GetProperty("data").GetProperty("colorValue").GetString()
                    .ShouldBe(fixture.ColorValue);
                result.GetProperty("isIncomplete").GetBoolean().ShouldBeTrue();
            }

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
            client.ErrorOutput.ShouldBeEmpty();
        }
        catch (Exception exception) when (exception is not TimeoutException)
        {
            throw new InvalidOperationException(
                $"The class-catalog process session failed. Server stderr:\n{client.ErrorOutput}",
                exception);
        }
        finally
        {
            foreach (var notification in notifications)
            {
                notification.Dispose();
            }
        }
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

    private static string CreateCompletionMessage(
        string documentUri,
        (int Line, int Character) position)
        => JsonSerializer.Serialize(
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
                        line = position.Line,
                        character = position.Character,
                    },
                },
            });

    private static (int Line, int Character) GetPosition(string text, int offset)
    {
        var line = 0;
        var character = 0;
        for (var index = 0; index < offset; index++)
        {
            if (text[index] == '\n')
            {
                line++;
                character = 0;
            }
            else
            {
                character++;
            }
        }

        return (line, character);
    }

    private sealed record Fixture(
        string ServerExecutable,
        string DocumentPath,
        string ClassName,
        string CompletionPrefix,
        string ColorValue)
    {
        internal static Fixture? TryReadFromEnvironment()
        {
            var fixturePath = Environment.GetEnvironmentVariable(
                "VIU_CLASS_CATALOG_FIXTURE");
            if (string.IsNullOrWhiteSpace(fixturePath))
            {
                return null;
            }

            using var document = JsonDocument.Parse(File.ReadAllBytes(fixturePath));
            var root = document.RootElement;
            var fixture = new Fixture(
                root.GetProperty("serverExecutable").GetString()!,
                root.GetProperty("documentPath").GetString()!,
                root.GetProperty("className").GetString()!,
                root.GetProperty("completionPrefix").GetString()!,
                root.GetProperty("colorValue").GetString()!);
            if (!File.Exists(fixture.ServerExecutable))
            {
                throw new FileNotFoundException(
                    "The class-catalog fixture's language-server executable does not exist.",
                    fixture.ServerExecutable);
            }

            if (!File.Exists(fixture.DocumentPath))
            {
                throw new FileNotFoundException(
                    "The class-catalog fixture's Viu document does not exist.",
                    fixture.DocumentPath);
            }

            return fixture;
        }
    }
}
