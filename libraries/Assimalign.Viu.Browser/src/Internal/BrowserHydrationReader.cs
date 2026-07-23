using System;
using System.Collections.Generic;
using System.Globalization;

using Assimalign.Viu;

namespace Assimalign.Viu.Browser;

/// <summary>
/// The browser <see cref="HydrationNodeReader{TNode}"/> — decodes the single batched snapshot the JS
/// bridge produces (<c>dom.snapshotHydration</c>) and answers every hydration read from it, so the
/// client-side walk crosses the interop boundary once per hydration root rather than a marshaled call
/// per <c>firstChild</c>/<c>nextSibling</c>/<c>getAttribute</c> (the JS-interop cost discipline the
/// SSR area requires — see this package's <c>docs/DESIGN.md</c>). Node handles in the snapshot are the
/// same int handles the write-side node-ops use, so an adopted node's <see cref="int"/> flows straight
/// into <c>patchProp</c>/<c>insert</c>/<c>remove</c>. The "no node" sentinel is <c>0</c> (the bridge's
/// reserved handle), matching <see cref="RendererOptions{TNode}"/>'s <c>default</c> convention.
/// </summary>
internal sealed class BrowserHydrationReader : HydrationNodeReader<int>
{
    private readonly Dictionary<int, SnapshotNode> _nodes;

    internal BrowserHydrationReader(string snapshot)
    {
        _nodes = Parse(snapshot);
        foreach (int handle in _nodes.Keys)
        {
            MaximumHandle = Math.Max(MaximumHandle, handle);
        }
    }

    /// <summary>
    /// Gets the largest bridge handle in this snapshot so buffered allocation can advance beyond
    /// every adopted server node before mismatch recovery creates a client node.
    /// </summary>
    internal int MaximumHandle { get; }

    /// <inheritdoc/>
    public override HydrationNodeKind Kind(int node)
        => _nodes.TryGetValue(node, out var entry) ? entry.Kind : HydrationNodeKind.Other;

    /// <inheritdoc/>
    public override int FirstChild(int node) => _nodes.TryGetValue(node, out var entry) ? entry.FirstChild : 0;

    /// <inheritdoc/>
    public override int NextSibling(int node) => _nodes.TryGetValue(node, out var entry) ? entry.NextSibling : 0;

    /// <inheritdoc/>
    public override int ParentNode(int node) => _nodes.TryGetValue(node, out var entry) ? entry.Parent : 0;

    /// <inheritdoc/>
    public override string ElementTag(int node) => _nodes.TryGetValue(node, out var entry) ? entry.Tag : string.Empty;

    /// <inheritdoc/>
    public override string Data(int node) => _nodes.TryGetValue(node, out var entry) ? entry.Data : string.Empty;

    /// <inheritdoc/>
    public override string? Attribute(int node, string name)
        => _nodes.TryGetValue(node, out var entry) && entry.Attributes is { } attributes
            && attributes.TryGetValue(name, out var value)
            ? value
            : null;

    private static Dictionary<int, SnapshotNode> Parse(string snapshot)
    {
        var cursor = 0;
        var count = ReadInt(snapshot, ref cursor);
        var nodes = new Dictionary<int, SnapshotNode>(count);
        for (var index = 0; index < count; index++)
        {
            var handle = ReadInt(snapshot, ref cursor);
            var parent = ReadInt(snapshot, ref cursor);
            var firstChild = ReadInt(snapshot, ref cursor);
            var nextSibling = ReadInt(snapshot, ref cursor);
            var kind = ReadInt(snapshot, ref cursor);
            string tag = string.Empty;
            string data = string.Empty;
            Dictionary<string, string>? attributes = null;
            if (kind == 0)
            {
                tag = ReadString(snapshot, ref cursor);
                var attributeCount = ReadInt(snapshot, ref cursor);
                if (attributeCount > 0)
                {
                    attributes = new Dictionary<string, string>(attributeCount, StringComparer.Ordinal);
                    for (var attributeIndex = 0; attributeIndex < attributeCount; attributeIndex++)
                    {
                        var name = ReadString(snapshot, ref cursor);
                        var value = ReadString(snapshot, ref cursor);
                        attributes[name] = value;
                    }
                }
            }
            else
            {
                data = ReadString(snapshot, ref cursor);
            }
            nodes[handle] = new SnapshotNode
            {
                Kind = kind switch
                {
                    0 => HydrationNodeKind.Element,
                    1 => HydrationNodeKind.Text,
                    2 => HydrationNodeKind.Comment,
                    _ => HydrationNodeKind.Other,
                },
                Parent = parent,
                FirstChild = firstChild,
                NextSibling = nextSibling,
                Tag = tag,
                Data = data,
                Attributes = attributes,
            };
        }
        return nodes;
    }

    private static int ReadInt(string snapshot, ref int cursor)
    {
        var start = cursor;
        while (cursor < snapshot.Length && snapshot[cursor] != ' ')
        {
            cursor++;
        }
        var value = int.Parse(snapshot.AsSpan(start, cursor - start), NumberStyles.Integer, CultureInfo.InvariantCulture);
        cursor++; // skip the terminating space
        return value;
    }

    private static string ReadString(string snapshot, ref int cursor)
    {
        var lengthStart = cursor;
        while (cursor < snapshot.Length && snapshot[cursor] != ':')
        {
            cursor++;
        }
        var length = int.Parse(snapshot.AsSpan(lengthStart, cursor - lengthStart), NumberStyles.Integer, CultureInfo.InvariantCulture);
        cursor++; // skip ':'
        var value = snapshot.Substring(cursor, length);
        cursor += length + 1; // skip the content and the terminating space
        return value;
    }

    private sealed class SnapshotNode
    {
        public required HydrationNodeKind Kind { get; init; }

        public required int Parent { get; init; }

        public required int FirstChild { get; init; }

        public required int NextSibling { get; init; }

        public required string Tag { get; init; }

        public required string Data { get; init; }

        public required Dictionary<string, string>? Attributes { get; init; }
    }
}
