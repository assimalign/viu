using System.Collections.Generic;
using System.Globalization;

using Assimalign.Viu.Components;
using Assimalign.Viu.Shared;

namespace Assimalign.Viu.Testing.Benchmarks;

/// <summary>
/// Turns a row list into the component tree the renderer diffs — the shape js-framework-benchmark
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
    /// <returns>The table component.</returns>
    public static IElementComponent Build(
        IReadOnlyList<BenchmarkRow> rows,
        int? selectedIdentifier,
        ScenarioVariant variant)
    {
        var rowComponents = new IComponent[rows.Count];
        for (var index = 0; index < rows.Count; index++)
        {
            rowComponents[index] = BuildRow(rows[index], selectedIdentifier, variant);
        }

        var body = ComponentTree.Element("tbody", children: rowComponents);
        return ComponentTree.Element(
            "table",
            Attributes(("class", "table table-hover table-striped test-data")),
            [body]);
    }

    private static IElementComponent BuildRow(BenchmarkRow row, int? selectedIdentifier, ScenarioVariant variant)
    {
        var keyed = variant == ScenarioVariant.Optimized;
        var isSelected = selectedIdentifier == row.Identifier;
        var identifierText = row.Identifier.ToString(CultureInfo.InvariantCulture);

        // The label link is the one dynamic cell: in the optimized variant it carries PatchFlags.Text so
        // a label change is a single targeted set-text; the keyless variant leaves it a plain text
        // element (a direct text change still costs one set-text, but the row loses its diff key).
        var labelLink = ComponentTree.Element(
            "a",
            Attributes(("class", "lbl")),
            [ComponentTree.Text(row.Label)],
            optimization: keyed ? new ComponentOptimization(PatchFlags.Text) : null);

        var cells = new IComponent[]
        {
            ComponentTree.Element(
                "td",
                Attributes(("class", "col-md-1")),
                [ComponentTree.Text(identifierText)]),
            ComponentTree.Element("td", Attributes(("class", "col-md-4")), [labelLink]),
            ComponentTree.Element(
                "td",
                Attributes(("class", "col-md-1")),
                [
                    ComponentTree.Element(
                        "a",
                        Attributes(("class", "remove")),
                        [
                            ComponentTree.Element(
                                "span",
                                Attributes(("class", "glyphicon glyphicon-remove"), ("aria-hidden", "true"))),
                        ]),
                ]),
            ComponentTree.Element("td", Attributes(("class", "col-md-6"))),
        };

        // No optimization flag on the row itself: a full attribute diff of an unchanged row emits no
        // operations, so the row's own crossings stay minimal without a block tree, while the keyed diff
        // (driven by the key parameter) still gets to move rows instead of rebuilding them. Selection
        // toggles the class only.
        return ComponentTree.Element(
            "tr",
            isSelected ? Attributes(("class", "danger")) : null,
            cells,
            key: keyed ? row.Identifier : null);
    }

    private static ComponentAttributes Attributes(
        params (string Name, object? Value)[] values)
    {
        var attributes = new List<IComponentAttribute>(values.Length);
        for (var index = 0; index < values.Length; index++)
        {
            attributes.Add(new ComponentAttribute(values[index].Name, values[index].Value));
        }

        return new ComponentAttributes(attributes);
    }
}
