using System.Collections.Generic;

using Shouldly;

using Xunit;

using Assimalign.Viu.VisualStudio;

namespace Assimalign.Viu.VisualStudio.Tests;

/// <summary>
/// Pins publication ordering, checksum gating, path matching, close teardown, and weak-listener
/// notification without loading any Visual Studio editor assembly. [V01.01.12.07.11]
/// </summary>
public class ViuSemanticClassificationStateTests
{
    [Fact]
    public void TryGetClassifications_MatchingPathAndChecksum_ReturnsExactNames()
    {
        var state = new ViuSemanticClassificationState();
        IReadOnlyList<ViuSemanticClassification> expected =
        [
            new ViuSemanticClassification(2, 8, 2, 13, "local name"),
        ];
        state.Publish(
            new ViuSemanticClassificationPublication(
                "file:///C:/workspace/Card.viu",
                3,
                "checksum-3",
                false,
                expected));

        bool found = state.TryGetClassifications(
            @"c:\workspace\CARD.viu",
            "checksum-3",
            out IReadOnlyList<ViuSemanticClassification> classifications);

        found.ShouldBeTrue();
        classifications.ShouldBe(expected);
    }

    [Fact]
    public void TryGetClassifications_TextChangedBeforePublication_PreservesLexicalFallback()
    {
        var state = new ViuSemanticClassificationState();
        state.Publish(
            Publication(
                version: 1,
                checksum: "old-text",
                classificationTypeName: "field name"));

        bool found = state.TryGetClassifications(
            @"C:\workspace\Card.viu",
            "new-text",
            out IReadOnlyList<ViuSemanticClassification> classifications);

        found.ShouldBeFalse();
        classifications.ShouldBeEmpty();
    }

    [Fact]
    public void Publish_OlderVersionArrivesLater_DoesNotReplaceNewestSnapshot()
    {
        var state = new ViuSemanticClassificationState();
        state.Publish(Publication(5, "newest", "property name")).ShouldBeTrue();

        bool accepted = state.Publish(Publication(4, "older", "field name"));

        accepted.ShouldBeFalse();
        state.TryGetClassifications(
                @"C:\workspace\Card.viu",
                "newest",
                out IReadOnlyList<ViuSemanticClassification> classifications)
            .ShouldBeTrue();
        classifications.ShouldContain(classification =>
            classification.ClassificationTypeName == "property name");
    }

    [Fact]
    public void Publish_Close_RemovesSnapshotAndNotifiesDocumentListener()
    {
        var state = new ViuSemanticClassificationState();
        var listener = new RecordingListener();
        state.Subscribe(@"C:\workspace\Card.viu", listener);
        state.Publish(Publication(2, "open", "parameter name"));

        bool accepted = state.Publish(
            new ViuSemanticClassificationPublication(
                "file:///C:/workspace/Card.viu",
                null,
                null,
                true,
                []));

        accepted.ShouldBeTrue();
        state.TryGetClassifications(
                @"C:\workspace\Card.viu",
                "open",
                out IReadOnlyList<ViuSemanticClassification> classifications)
            .ShouldBeFalse();
        classifications.ShouldBeEmpty();
        listener.NotificationCount.ShouldBe(2);
    }

    [Fact]
    public void ClearPublications_NewServerSession_DropsOldVersionAndNotifiesListener()
    {
        var state = new ViuSemanticClassificationState();
        var listener = new RecordingListener();
        state.Subscribe(@"C:\workspace\Card.viu", listener);
        state.Publish(Publication(42, "old-session", "property name"));

        state.ClearPublications();
        bool accepted = state.Publish(Publication(1, "new-session", "local name"));

        accepted.ShouldBeTrue();
        state.TryGetClassifications(
                @"C:\workspace\Card.viu",
                "old-session",
                out IReadOnlyList<ViuSemanticClassification> stale)
            .ShouldBeFalse();
        stale.ShouldBeEmpty();
        state.TryGetClassifications(
                @"C:\workspace\Card.viu",
                "new-session",
                out IReadOnlyList<ViuSemanticClassification> current)
            .ShouldBeTrue();
        current.ShouldContain(classification =>
            classification.ClassificationTypeName == "local name");
        listener.NotificationCount.ShouldBe(3);
    }

    private static ViuSemanticClassificationPublication Publication(
        int version,
        string checksum,
        string classificationTypeName)
        => new(
            "file:///C:/workspace/Card.viu",
            version,
            checksum,
            false,
            [new ViuSemanticClassification(1, 4, 1, 9, classificationTypeName)]);

    private sealed class RecordingListener : IViuSemanticClassificationListener
    {
        internal int NotificationCount { get; private set; }

        public void OnSemanticClassificationsChanged(string documentIdentifier) =>
            this.NotificationCount++;
    }
}
