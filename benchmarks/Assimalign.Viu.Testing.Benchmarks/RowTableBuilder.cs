using System.Collections.Generic;
using System.Globalization;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Testing.Benchmarks;

/// <summary>
/// Turns a row list into the component tree the renderer diffs — the shape js-framework-benchmark
/// renders (a <c>&lt;table&gt;</c> of keyed <c>&lt;tr&gt;</c> rows, each with an id cell and a label
/// link; https://github.com/krausest/js-framework-benchmark). The <see cref="ScenarioVariant"/> chooses
/// between the framework's real output (keyed rows, a <see cref="PatchFlags.Text"/> label so a change is
/// one targeted set-text) and the keyless bypass the interop-count gate is proven against. A fresh tree
/// is built per frame because that is exactly what the runtime does: a render function re-creates its
/// component tree on every update, and the diff — not tree reuse — is what keeps the interop cost down.
/// Reusing trees here would measure a code path the framework never takes.
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
    /// <returns>The immutable table node.</returns>
    public static VirtualNode Build(
        IReadOnlyList<BenchmarkRow> rows,
        int? selectedIdentifier,
        ScenarioVariant variant)
    {
        var rowNodes = new VirtualNode[rows.Count];
        for (var index = 0; index < rows.Count; index++)
        {
            rowNodes[index] = BuildRow(rows[index], selectedIdentifier, variant);
        }

        ElementNode body = new(
            new QualifiedName("tbody"),
            children: rowNodes);
        return new ElementNode(
            new QualifiedName("table"),
            bindings: Attributes(("class", "table table-hover table-striped test-data")),
            children: [body]);
    }

    private static ElementNode BuildRow(
        BenchmarkRow row,
        int? selectedIdentifier,
        ScenarioVariant variant)
    {
        var keyed = variant == ScenarioVariant.Optimized;
        var isSelected = selectedIdentifier == row.Identifier;
        var identifierText = row.Identifier.ToString(CultureInfo.InvariantCulture);

        // The label link is the one dynamic cell: in the optimized variant it carries PatchFlags.Text so
        // a label change is a single targeted set-text; the keyless variant leaves it a plain text
        // element (a direct text change still costs one set-text, but the row loses its diff key).
        ElementNode labelLink = new(
            new QualifiedName("a"),
            bindings: Attributes(("class", "lbl")),
            children: [new TextNode(row.Label)],
            renderPlan: keyed ? new RenderPlan(PatchFlags.Text) : null);

        VirtualNode[] cells =
        {
            new ElementNode(
                new QualifiedName("td"),
                bindings: Attributes(("class", "col-md-1")),
                children: [new TextNode(identifierText)]),
            new ElementNode(
                new QualifiedName("td"),
                bindings: Attributes(("class", "col-md-4")),
                children: [labelLink]),
            new ElementNode(
                new QualifiedName("td"),
                bindings: Attributes(("class", "col-md-1")),
                children:
                [
                    new ElementNode(
                        new QualifiedName("a"),
                        bindings: Attributes(("class", "remove")),
                        children:
                        [
                            new ElementNode(
                                new QualifiedName("span"),
                                bindings: Attributes(
                                    ("class", "glyphicon glyphicon-remove"),
                                    ("aria-hidden", "true"))),
                        ]),
                ]),
            new ElementNode(
                new QualifiedName("td"),
                bindings: Attributes(("class", "col-md-6"))),
        };

        // No optimization flag on the row itself: a full attribute diff of an unchanged row emits no
        // operations, so the row's own crossings stay minimal without a block tree, while the keyed diff
        // (driven by the key parameter) still gets to move rows instead of rebuilding them. Selection
        // toggles the class only.
        return new ElementNode(
            new QualifiedName("tr"),
            bindings: isSelected ? Attributes(("class", "danger")) : null,
            children: cells,
            key: keyed ? row.Identifier : null);
    }

    private static IReadOnlyList<ElementBinding> Attributes(
        params (string Name, object? Value)[] values)
    {
        var attributes = new ElementBinding[values.Length];
        for (var index = 0; index < values.Length; index++)
        {
            attributes[index] = ElementBinding.Attribute(
                new QualifiedName(values[index].Name),
                values[index].Value);
        }

        return attributes;
    }
}
