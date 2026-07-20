using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Assimalign.Viu.RuntimeDom.Tests;

// A DOM-free, int-handle in-memory DOM that mirrors viu-dom.js faithfully: a handle -> node
// registry, deterministic subtree release on remove/setElementText, and the create/insert/text/leaf
// ops the renderer drives. It is the shared "device" both the DIRECT node-ops and the command-buffer
// DECODER drive in the differential test ([V01.01.04.05]) — the only variable under test is direct
// calls vs. encode/decode/replay, so byte-identical Serialize() output proves the buffer round-trips
// every op with its exact arguments. Serialize() emits a deterministic, total fingerprint of node
// state (attributes/props/styles/listeners sorted), not merely visible HTML, so any divergence shows.
internal sealed class InMemoryHandleDom
{
    private readonly Dictionary<int, Node> _nodes = [];
    private int _nextHandle = 1; // mirrors the JS registry; 0 is the "no node" sentinel.

    internal int NodeCount => _nodes.Count;

    // --- allocation (direct path allocates here; buffered path pre-allocates and registers AS) -----

    internal int CreateElement(string tag, string? elementNamespace)
        => Register(new Node(NodeKind.Element, tag) { ElementNamespace = elementNamespace });

    internal int CreateText(string text) => Register(new Node(NodeKind.Text, "#text") { Text = text });

    internal int CreateComment(string text) => Register(new Node(NodeKind.Comment, "#comment") { Text = text });

    internal void CreateElementAs(int handle, string tag, string? elementNamespace)
        => RegisterAs(handle, new Node(NodeKind.Element, tag) { ElementNamespace = elementNamespace });

    internal void CreateTextAs(int handle, string text)
        => RegisterAs(handle, new Node(NodeKind.Text, "#text") { Text = text });

    internal void CreateCommentAs(int handle, string text)
        => RegisterAs(handle, new Node(NodeKind.Comment, "#comment") { Text = text });

    // --- structural ------------------------------------------------------------------------------

    internal void Insert(int parentHandle, int childHandle, int anchorHandle)
    {
        var parent = Get(parentHandle);
        var child = Get(childHandle);
        if (child.Parent != 0 && _nodes.TryGetValue(child.Parent, out var oldParent))
        {
            oldParent.Children.Remove(childHandle);
        }
        child.Parent = parentHandle;
        if (anchorHandle == 0)
        {
            parent.Children.Add(childHandle);
            return;
        }
        var index = parent.Children.IndexOf(anchorHandle);
        parent.Children.Insert(index < 0 ? parent.Children.Count : index, childHandle);
    }

    internal int[] Remove(int childHandle)
    {
        var child = Get(childHandle);
        if (child.Parent != 0 && _nodes.TryGetValue(child.Parent, out var parent))
        {
            parent.Children.Remove(childHandle);
        }
        child.Parent = 0;
        var released = new List<int>();
        ReleaseSubtree(childHandle, released);
        return [.. released];
    }

    internal int[] SetElementText(int handle, string text)
    {
        var element = Get(handle);
        var released = new List<int>();
        foreach (var childHandle in element.Children.ToArray())
        {
            ReleaseSubtree(childHandle, released);
        }
        element.Children.Clear();
        // Content becomes a single text run; model it as the element's own text (serialized inline).
        element.Text = text;
        return [.. released];
    }

    internal void SetText(int handle, string text) => Get(handle).Text = text;

    internal int ParentNode(int handle)
    {
        var parent = Get(handle).Parent;
        return parent;
    }

    internal int NextSibling(int handle)
    {
        var node = Get(handle);
        if (node.Parent == 0 || !_nodes.TryGetValue(node.Parent, out var parent))
        {
            return 0;
        }
        var index = parent.Children.IndexOf(handle);
        return index >= 0 && index + 1 < parent.Children.Count ? parent.Children[index + 1] : 0;
    }

    internal (int First, int Last) InsertStaticContent(string content, int parentHandle, int anchorHandle, string? elementNamespace)
    {
        // One raw node stands in for the parsed chunk (parity with @vue/runtime-test): the buffered
        // path forces a flush and calls this exactly as the direct path does, so both DOMs match.
        var handle = Register(new Node(NodeKind.RawStatic, "#static") { Text = content, ElementNamespace = elementNamespace });
        Insert(parentHandle, handle, anchorHandle);
        return (handle, handle);
    }

    // --- leaf appliers (mirror BrowserPropertyLeafOperations 1:1) --------------------------------

    internal void SetAttribute(int handle, string name, string value) => Get(handle).Attributes[name] = value;

    internal void RemoveAttribute(int handle, string name) => Get(handle).Attributes.Remove(name);

    internal void SetXlinkAttribute(int handle, string name, string value) => Get(handle).Attributes[name] = value;

    internal void RemoveXlinkAttribute(int handle, string name) => Get(handle).Attributes.Remove(name);

    internal void SetClassName(int handle, string value) => Get(handle).Attributes["class"] = value;

    internal void SetStringProperty(int handle, string name, string value) => Get(handle).Properties[name] = value;

    internal void SetBooleanProperty(int handle, string name, bool value) => Get(handle).BooleanProperties[name] = value;

    internal void SetValueGuarded(int handle, string value)
    {
        var node = Get(handle);
        if (!string.Equals(node.Value, value, StringComparison.Ordinal))
        {
            node.Value = value;
            node.ValueWriteCount++;
        }
    }

    internal void SetStyleText(int handle, string cssText) => Get(handle).StyleText = cssText;

    internal void SetStyleProperty(int handle, string name, string value, bool important)
        => Get(handle).Styles[name] = important ? value + " !important" : value;

    internal void RemoveStyleProperty(int handle, string name) => Get(handle).Styles.Remove(name);

    internal void AddEventListener(int handle, string eventName, bool once, bool capture, bool passive)
        => Get(handle).Listeners[capture ? eventName + "|capture" : eventName] = $"once={once},passive={passive}";

    internal void RemoveEventListener(int handle, string eventName, bool capture)
        => Get(handle).Listeners.Remove(capture ? eventName + "|capture" : eventName);

    // --- transition class choreography ([V01.01.04.07.02]) ---------------------------------------
    // Mirrors viu-dom.js addTransitionClass/removeTransitionClass (each whitespace-separated token joins
    // el.classList, tracked in the el.__vtc-style set) and forceReflow (document.body.offsetHeight). The
    // reflow read is counted so a sequencing test can assert the barrier fired between class writes.

    internal int ReflowCount { get; private set; }

    /// <summary>
    /// The ordered <c>add:</c>/<c>remove:</c>/<c>reflow</c> log the decoder appends as it replays a frame,
    /// in exact op order — the browser-observable class/reflow sequence a sequencing test slices per apply.
    /// </summary>
    internal List<string> TransitionLog { get; } = [];

    internal void AddTransitionClass(int handle, string cssClass)
    {
        TransitionLog.Add("add:" + cssClass);
        var classes = Get(handle).TransitionClasses;
        foreach (var token in cssClass.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            classes.Add(token);
        }
    }

    internal void RemoveTransitionClass(int handle, string cssClass)
    {
        TransitionLog.Add("remove:" + cssClass);
        var classes = Get(handle).TransitionClasses;
        foreach (var token in cssClass.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            classes.Remove(token);
        }
    }

    /// <summary>The reflow barrier: counts a real synchronous reflow so tests can pin the barrier's position.</summary>
    internal void ForceReflow()
    {
        TransitionLog.Add("reflow");
        ReflowCount++;
    }

    // FLIP move ops ([V01.01.04.07.03]): mirror viu-dom.js setMoveTransform (inline transform +
    // transitionDuration:0s) and clearMoveStyles. Kept out of the structural Serialize() fingerprint like
    // the transition classes; the ordered log lets a sequencing test pin transforms -> reflow -> class -> clear.

    /// <summary>Records the FLIP inverting transform for an element (upstream <c>applyTranslation</c>).</summary>
    internal void SetMoveTransform(int handle, double deltaX, double deltaY)
    {
        TransitionLog.Add(string.Create(CultureInfo.InvariantCulture, $"transform:{handle}:{deltaX},{deltaY}"));
        Get(handle).MoveTransform = (deltaX, deltaY);
    }

    /// <summary>Clears the FLIP transform so the move class animates the element home (upstream <c>clearMoveStyles</c>).</summary>
    internal void ClearMoveStyles(int handle)
    {
        TransitionLog.Add(string.Create(CultureInfo.InvariantCulture, $"clear:{handle}"));
        Get(handle).MoveTransform = null;
    }

    /// <summary>The FLIP inverting transform currently applied to an element, or null once cleared.</summary>
    internal (double DeltaX, double DeltaY)? MoveTransform(int handle) => Get(handle).MoveTransform;

    /// <summary>The transition classes currently on an element (upstream <c>el.__vtc</c>/<c>classList</c>).</summary>
    internal IReadOnlyCollection<string> TransitionClasses(int handle) => Get(handle).TransitionClasses;

    /// <summary>Whether the handle is still registered (not yet released by a remove/setElementText).</summary>
    internal bool IsMounted(int handle) => _nodes.ContainsKey(handle);

    /// <summary>The handle of the first live element with <paramref name="tag"/> in creation order, or 0.</summary>
    internal int FindFirstElement(string tag)
    {
        for (var handle = 1; handle < _nextHandle; handle++)
        {
            if (_nodes.TryGetValue(handle, out var node) && node.Kind == NodeKind.Element
                && string.Equals(node.Tag, tag, StringComparison.Ordinal))
            {
                return handle;
            }
        }
        return 0;
    }

    // --- serialization (deterministic total fingerprint) -----------------------------------------

    internal string Serialize(int handle)
    {
        var builder = new StringBuilder();
        SerializeNode(builder, handle);
        return builder.ToString();
    }

    private void SerializeNode(StringBuilder builder, int handle)
    {
        var node = Get(handle);
        switch (node.Kind)
        {
            case NodeKind.Text:
                builder.Append(node.Text);
                return;
            case NodeKind.Comment:
                builder.Append("<!--").Append(node.Text).Append("-->");
                return;
            case NodeKind.RawStatic:
                builder.Append("[static:").Append(node.ElementNamespace ?? "html").Append(':').Append(node.Text).Append(']');
                return;
        }
        builder.Append('<').Append(node.Tag);
        foreach (var (name, value) in node.Attributes.OrderBy(static entry => entry.Key, StringComparer.Ordinal))
        {
            builder.Append(' ').Append(name).Append("=\"").Append(value).Append('"');
        }
        foreach (var (name, value) in node.Properties.OrderBy(static entry => entry.Key, StringComparer.Ordinal))
        {
            builder.Append(" .").Append(name).Append("=\"").Append(value).Append('"');
        }
        foreach (var (name, value) in node.BooleanProperties.OrderBy(static entry => entry.Key, StringComparer.Ordinal))
        {
            builder.Append(" ?").Append(name).Append('=').Append(value ? "true" : "false");
        }
        if (node.StyleText is not null)
        {
            builder.Append(" style.cssText=\"").Append(node.StyleText).Append('"');
        }
        foreach (var (name, value) in node.Styles.OrderBy(static entry => entry.Key, StringComparer.Ordinal))
        {
            builder.Append(" style.").Append(name).Append("=\"").Append(value).Append('"');
        }
        if (node.Value is not null)
        {
            builder.Append(string.Create(CultureInfo.InvariantCulture, $" value=\"{node.Value}\"@{node.ValueWriteCount}"));
        }
        foreach (var key in node.Listeners.Keys.OrderBy(static key => key, StringComparer.Ordinal))
        {
            builder.Append(" @").Append(key);
        }
        builder.Append('>');
        if (node.Text.Length > 0 && node.Children.Count == 0)
        {
            builder.Append(node.Text);
        }
        foreach (var childHandle in node.Children)
        {
            SerializeNode(builder, childHandle);
        }
        builder.Append("</").Append(node.Tag).Append('>');
    }

    // --- registry --------------------------------------------------------------------------------

    private int Register(Node node)
    {
        var handle = _nextHandle++;
        _nodes[handle] = node;
        return handle;
    }

    private void RegisterAs(int handle, Node node)
    {
        _nodes[handle] = node;
        if (handle >= _nextHandle)
        {
            _nextHandle = handle + 1;
        }
    }

    private Node Get(int handle)
        => _nodes.TryGetValue(handle, out var node)
            ? node
            : throw new InvalidOperationException($"Unknown DOM handle {handle}.");

    private void ReleaseSubtree(int handle, List<int> released)
    {
        if (!_nodes.TryGetValue(handle, out var node))
        {
            return;
        }
        released.Add(handle);
        foreach (var childHandle in node.Children)
        {
            ReleaseSubtree(childHandle, released);
        }
        _nodes.Remove(handle);
    }

    private enum NodeKind
    {
        Element,
        Text,
        Comment,
        RawStatic,
    }

    private sealed class Node(NodeKind kind, string tag)
    {
        internal NodeKind Kind { get; } = kind;

        internal string Tag { get; } = tag;

        internal string? ElementNamespace { get; init; }

        internal string Text { get; set; } = string.Empty;

        internal string? Value { get; set; }

        internal int ValueWriteCount { get; set; }

        internal string? StyleText { get; set; }

        internal int Parent { get; set; }

        internal List<int> Children { get; } = [];

        internal Dictionary<string, string> Attributes { get; } = new(StringComparer.Ordinal);

        internal Dictionary<string, string> Properties { get; } = new(StringComparer.Ordinal);

        internal Dictionary<string, bool> BooleanProperties { get; } = new(StringComparer.Ordinal);

        internal Dictionary<string, string> Styles { get; } = new(StringComparer.Ordinal);

        internal Dictionary<string, string> Listeners { get; } = new(StringComparer.Ordinal);

        // Transition classes are tracked apart from the bound `class` attribute (upstream el.__vtc), so
        // they stay out of the structural Serialize() fingerprint the differential test compares.
        internal HashSet<string> TransitionClasses { get; } = new(StringComparer.Ordinal);

        // The FLIP inverting transform (upstream el.style.transform), likewise out of the fingerprint.
        internal (double DeltaX, double DeltaY)? MoveTransform { get; set; }
    }
}
