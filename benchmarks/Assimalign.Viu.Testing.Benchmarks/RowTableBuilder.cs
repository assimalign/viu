using System.Collections.Generic;
using System.Globalization;

using Assimalign.Viu;
using Assimalign.Viu.Shared;

namespace Assimalign.Viu.Testing.Benchmarks;

/// <summary>
/// Turns a row list into the virtual-node tree the renderer diffs — the shape js-framework-benchmark
/// renders (a <c>&lt;table&gt;</c> of keyed <c>&lt;tr&gt;</c> rows, each with an id cell and a label
/// link; https://github.com/krausest/js-framework-benchmark). The <see cref="ScenarioVariant"/> chooses
/// between the framework's real output (keyed rows, a <see cref="PatchFlags.Text"/> label so a change is
/// one targeted set-text) and the keyless bypass the interop-count gate is proven against. Building a
/// fresh tree per frame mirrors Vue, whose render function re-creates vnodes every update.
/// </summary>
public static class RowTableBuilder
{
    /// <summary>
    /// Builds the table for <paramref name="rows"/>, marking the row whose id equals
    /// <paramref name="selectedIdentifier"/> (if any) selected.
    /// </summary>
    /// <param name="rows">The rows to render.</param>
    /// <param name="selectedIdentifier">The selected row id, or null for none.</param>
    /// <param name="variant">Keyed+flagged output, or the keyless bypass.</param>
    /// <returns>The table vnode.</returns>
    public static VirtualNode Build(
        IReadOnlyList<BenchmarkRow> rows,
        int? selectedIdentifier,
        ScenarioVariant variant)
    {
        var rowNodes = new VirtualNode?[rows.Count];
        for (var index = 0; index < rows.Count; index++)
        {
            rowNodes[index] = BuildRow(rows[index], selectedIdentifier, variant);
        }

        var body = VirtualNodeFactory.Element("tbody", null, rowNodes);
        return VirtualNodeFactory.Element(
            "table",
            VirtualNodeFactory.Properties(("class", "table table-hover table-striped test-data")),
            body);
    }

    private static VirtualNode BuildRow(BenchmarkRow row, int? selectedIdentifier, ScenarioVariant variant)
    {
        var keyed = variant == ScenarioVariant.Optimized;
        var isSelected = selectedIdentifier == row.Identifier;
        var identifierText = row.Identifier.ToString(CultureInfo.InvariantCulture);

        // The label link is the one dynamic cell: in the optimized variant it carries PatchFlags.Text so
        // a label change is a single targeted set-text; the keyless variant leaves it a plain text
        // element (a direct text change still costs one set-text, but the row loses its diff key).
        var labelLink = keyed
            ? VirtualNodeFactory.Element("a", VirtualNodeFactory.Properties(("class", "lbl")), row.Label, PatchFlags.Text)
            : VirtualNodeFactory.Element("a", VirtualNodeFactory.Properties(("class", "lbl")), row.Label);

        var cells = new VirtualNode?[]
        {
            VirtualNodeFactory.Element("td", VirtualNodeFactory.Properties(("class", "col-md-1")), identifierText),
            VirtualNodeFactory.Element("td", VirtualNodeFactory.Properties(("class", "col-md-4")), labelLink),
            VirtualNodeFactory.Element(
                "td",
                VirtualNodeFactory.Properties(("class", "col-md-1")),
                VirtualNodeFactory.Element(
                    "a",
                    VirtualNodeFactory.Properties(("class", "remove")),
                    VirtualNodeFactory.Element(
                        "span",
                        VirtualNodeFactory.Properties(("class", "glyphicon glyphicon-remove"), ("aria-hidden", "true"))))),
            VirtualNodeFactory.Element("td", VirtualNodeFactory.Properties(("class", "col-md-6"))),
        };

        return VirtualNodeFactory.Element("tr", BuildRowProperties(row.Identifier, isSelected, keyed), cells);
    }

    private static VirtualNodeProperties? BuildRowProperties(int identifier, bool isSelected, bool keyed)
    {
        // No PatchFlag on the row itself: a full prop diff of an unchanged row emits no ops, so the row's
        // own crossings stay minimal without a block tree, while the keyed diff (driven by the "key"
        // prop) still gets to move rows instead of rebuilding them. Selection toggles the class only.
        if (keyed && isSelected)
        {
            return VirtualNodeFactory.Properties(("key", identifier), ("class", "danger"));
        }
        if (keyed)
        {
            return VirtualNodeFactory.Properties(("key", identifier));
        }
        if (isSelected)
        {
            return VirtualNodeFactory.Properties(("class", "danger"));
        }
        return null;
    }
}
