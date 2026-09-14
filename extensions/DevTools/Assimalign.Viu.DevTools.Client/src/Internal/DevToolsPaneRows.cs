using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using Assimalign.Viu.DevTools;

namespace Assimalign.Viu.DevTools.Client;

// Presentation traverses only protocol values already received; it never initiates expansion.
internal static class DevToolsPaneRows
{
    // ARIA and custom data attributes carry string values; boolean DOM properties use presence.
    internal static string BooleanAttribute(bool value) => value ? "true" : "false";

    internal sealed record TreeRow(DevToolsComponentNode Node, int Depth)
    {
        public string Indentation => "padding-inline-start: " + (Depth * 16).ToString(CultureInfo.InvariantCulture) + "px";
    }

    internal sealed record ValueRow(string Section, string Name, DevToolsValuePayload Value, int Depth)
    {
        public string Key => Section + ":" + EncodePath(Value.Path);

        public string Path => string.Join(".", Value.Path);

        public string Display => Value.DisplayValue ?? Value.Kind;

        public string Indentation => "padding-inline-start: " + (Depth * 16).ToString(CultureInfo.InvariantCulture) + "px";

        public bool Editable => Section == "state" && (Value.Kind == "scalar" || Value.Kind == "null");
    }

    internal sealed record InspectorRow(InspectorNodePayload Node, int Depth)
    {
        public string Indentation => "padding-inline-start: " + (Depth * 16).ToString(CultureInfo.InvariantCulture) + "px";
    }

    internal static IReadOnlyList<TreeRow> Tree(IReadOnlyList<DevToolsComponentNode> roots)
    {
        List<TreeRow> rows = [];
        Stack<TreeRow> pending = new();
        for (int index = roots.Count - 1; index >= 0; index--)
        {
            pending.Push(new TreeRow(roots[index], 0));
        }
        while (pending.TryPop(out TreeRow? row))
        {
            rows.Add(row);
            for (int index = row.Node.Children.Count - 1; index >= 0; index--)
            {
                pending.Push(new TreeRow(row.Node.Children[index], row.Depth + 1));
            }
        }
        return rows;
    }

    internal static IReadOnlyList<ValueRow> Snapshot(DevToolsClientSession session)
    {
        List<ValueRow> rows = [];
        if (session.Snapshot is not { } snapshot)
        {
            return rows;
        }
        AddValues(rows, "parameters", snapshot.Parameters, session.Expansions);
        AddValues(rows, "state", snapshot.State, session.Expansions);
        return rows;
    }

    internal static IReadOnlyList<ValueRow> InspectorState(InspectorStateResponsePayload? state)
    {
        List<ValueRow> rows = [];
        if (state is not null)
        {
            AddValues(rows, "inspector", state.State, []);
        }
        return rows;
    }

    internal static IReadOnlyList<InspectorRow> InspectorTree(InspectorTreeResponsePayload? tree)
    {
        List<InspectorRow> rows = [];
        if (tree is null)
        {
            return rows;
        }
        Stack<InspectorRow> pending = new();
        for (int index = tree.Roots.Count - 1; index >= 0; index--)
        {
            pending.Push(new InspectorRow(tree.Roots[index], 0));
        }
        while (pending.TryPop(out InspectorRow? row))
        {
            rows.Add(row);
            for (int index = row.Node.Children.Count - 1; index >= 0; index--)
            {
                pending.Push(new InspectorRow(row.Node.Children[index], row.Depth + 1));
            }
        }
        return rows;
    }

    private static void AddValues(
        List<ValueRow> rows,
        string section,
        IReadOnlyList<DevToolsNamedValuePayload> values,
        IReadOnlyList<ComponentExpansionResponsePayload> expansions)
    {
        Dictionary<string, DevToolsValuePayload> replacements = new(StringComparer.Ordinal);
        foreach (ComponentExpansionResponsePayload expansion in expansions)
        {
            if (expansion.Section == section && expansion.Value is not null)
            {
                replacements[EncodePath(expansion.Path)] = expansion.Value;
            }
        }
        Stack<ValueRow> pending = new();
        for (int index = values.Count - 1; index >= 0; index--)
        {
            pending.Push(new ValueRow(section, values[index].Name, values[index].Value, 0));
        }
        while (pending.TryPop(out ValueRow? row))
        {
            if (replacements.TryGetValue(EncodePath(row.Value.Path), out DevToolsValuePayload? replacement))
            {
                row = row with { Value = replacement };
            }
            rows.Add(row);
            for (int index = row.Value.Children.Count - 1; index >= 0; index--)
            {
                DevToolsNamedValuePayload child = row.Value.Children[index];
                pending.Push(new ValueRow(section, child.Name, child.Value, row.Depth + 1));
            }
        }
    }

    private static string EncodePath(IReadOnlyList<string> path)
    {
        StringBuilder encoded = new();
        foreach (string segment in path)
        {
            encoded.Append(segment.Length.ToString(CultureInfo.InvariantCulture));
            encoded.Append(':');
            encoded.Append(segment);
        }
        return encoded.ToString();
    }
}
