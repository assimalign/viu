using System.Linq;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.UtilityCss.Tests;

public sealed class UtilityCandidateProjectIndexTests
{
    [Fact]
    public void UpdateSource_DuplicateProjectReference_DoesNotInvalidateUnion()
    {
        var index = new UtilityCandidateProjectIndex();
        var first = index.UpdateSource(
            "components/first.vue",
            UtilityCandidateScanner.Scan(
                "flex gap-4"));

        var duplicate = index.UpdateSource(
            "components/second.viu",
            UtilityCandidateScanner.Scan(
                "gap-4"));

        first.AddedCandidates.ShouldBe(["flex", "gap-4"]);
        duplicate.IsChanged.ShouldBeFalse();
        index.CandidateTexts.ShouldBe(["flex", "gap-4"]);
    }

    [Fact]
    public void RemoveSource_FinalReference_RemovesOnlyUnreferencedCandidate()
    {
        var index = new UtilityCandidateProjectIndex();
        index.UpdateSource(
            "components/first.vue",
            UtilityCandidateScanner.Scan(
                "flex gap-4"));
        index.UpdateSource(
            "components/second.vue",
            UtilityCandidateScanner.Scan(
                "gap-4"));

        var firstRemoval = index.RemoveSource(
            "components/first.vue");
        var finalRemoval = index.RemoveSource(
            "components/second.vue");

        firstRemoval.RemovedCandidates.ShouldBe(["flex"]);
        finalRemoval.RemovedCandidates.ShouldBe(["gap-4"]);
        index.CandidateTexts.ShouldBeEmpty();
        index.SourceIdentities.ShouldBeEmpty();
    }

    [Fact]
    public void UpdateSource_OccurrenceOnlyChange_DoesNotInvalidateUnion()
    {
        var index = new UtilityCandidateProjectIndex();
        index.UpdateSource(
            "components/card.vue",
            UtilityCandidateScanner.Scan(
                "flex"));

        var change = index.UpdateSource(
            "components/card.vue",
            UtilityCandidateScanner.Scan(
                "flex flex"));

        change.IsChanged.ShouldBeFalse();
        index.CandidateTexts.Single().ShouldBe("flex");
    }

    [Fact]
    public void UpdateSource_Replacement_ReportsOrdinalAddedAndRemovedSets()
    {
        var index = new UtilityCandidateProjectIndex();
        index.UpdateSource(
            "components/card.vue",
            UtilityCandidateScanner.Scan(
                "flex gap-4"));

        var change = index.UpdateSource(
            "components/card.vue",
            UtilityCandidateScanner.Scan(
                "block p-2"));

        change.AddedCandidates.ShouldBe(["block", "p-2"]);
        change.RemovedCandidates.ShouldBe(["flex", "gap-4"]);
        index.CandidateTexts.ShouldBe(["block", "p-2"]);
    }
}
