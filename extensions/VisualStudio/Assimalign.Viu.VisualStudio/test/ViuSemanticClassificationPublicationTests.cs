using System;
using System.Text.Json;

using Shouldly;

using Xunit;

using Assimalign.Viu.VisualStudio;

namespace Assimalign.Viu.VisualStudio.Tests;

/// <summary>
/// Pins the editor-free JSON contract used by the thin Visual Studio semantic-classification
/// adapter. [V01.01.12.07.11]
/// </summary>
public class ViuSemanticClassificationPublicationTests
{
    [Fact]
    public void TryParse_ExactServerPublication_PreservesVersionChecksumNameAndRange()
    {
        using JsonDocument document = JsonDocument.Parse(
            """
            {
              "uri": "file:///C:/workspace/Card.viu",
              "version": 7,
              "textChecksum": "server-checksum",
              "isClosed": false,
              "classifications": [
                {
                  "classificationTypeName": "property name",
                  "range": {
                    "start": { "line": 4, "character": 11 },
                    "end": { "line": 4, "character": 16 }
                  }
                }
              ]
            }
            """);

        bool parsed = ViuSemanticClassificationPublication.TryParse(
            document.RootElement,
            out ViuSemanticClassificationPublication? publication);

        parsed.ShouldBeTrue();
        publication.ShouldNotBeNull();
        publication.DocumentIdentifier.ShouldBe("file:///C:/workspace/Card.viu");
        publication.Version.ShouldBe(7);
        publication.TextChecksum.ShouldBe("server-checksum");
        publication.IsClosed.ShouldBeFalse();
        publication.Classifications.ShouldBe(
        [
            new ViuSemanticClassification(4, 11, 4, 16, "property name"),
        ]);
    }

    [Theory]
    [InlineData(
        """
        {"uri":"file:///Card.viu","version":1,"isClosed":false,"classifications":[]}
        """)]
    [InlineData(
        """
        {"uri":"file:///Card.viu","version":1,"textChecksum":"x","isClosed":false,"classifications":[{"classificationTypeName":"local name","range":{"start":{"line":1,"character":3},"end":{"line":2,"character":4}}}]}
        """)]
    [InlineData(
        """
        {"uri":"file:///Card.viu","version":1,"textChecksum":"x","isClosed":false,"classifications":[{"classificationTypeName":"","range":{"start":{"line":1,"character":3},"end":{"line":1,"character":4}}}]}
        """)]
    [InlineData(
        """
        {"uri":"file:///Card.viu","version":1,"textChecksum":"x","isClosed":false,"classifications":[{"classificationTypeName":"local name","range":{"start":{"line":"one","character":3},"end":{"line":1,"character":4}}}]}
        """)]
    public void TryParse_InvalidOrUnsafePublication_IsRejected(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        ViuSemanticClassificationPublication.TryParse(document.RootElement, out _)
            .ShouldBeFalse();
    }

    [Fact]
    public void Compute_KnownText_UsesServerCompatibleUtf8Sha256()
    {
        ViuDocumentTextChecksum.Compute("Viu\n")
            .ShouldBe("cky5hMd4dzR9iJUWww+rP9qM9jfQl0FUB90suepnndI=");
    }

    [Fact]
    public void Receive_ServerNotification_PublishesExactClassificationForMatchingEditorText()
    {
        using JsonDocument document = JsonDocument.Parse(
            """
            {
              "uri": "file:///C:/workspace/Card.viu",
              "version": 8,
              "textChecksum": "matching-checksum",
              "isClosed": false,
              "classifications": [
                {
                  "classificationTypeName": "local name",
                  "range": {
                    "start": { "line": 7, "character": 12 },
                    "end": { "line": 7, "character": 17 }
                  }
                }
              ]
            }
            """);
        var state = new ViuSemanticClassificationState();
        var receiver = new ViuSemanticClassificationReceiver(state);

        bool received = receiver.Receive(document.RootElement);
        bool found = state.TryGetClassifications(
            @"C:\workspace\Card.viu",
            "matching-checksum",
            out var classifications);

        received.ShouldBeTrue();
        found.ShouldBeTrue();
        classifications.ShouldBe(
        [
            new ViuSemanticClassification(7, 12, 7, 17, "local name"),
        ]);
    }
}
