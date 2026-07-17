namespace Assimalign.Vue.RuntimeCore.VirtualDom;

public readonly record struct NodePath
{
    private readonly int[] _segments;

    public static NodePath Root { get; } = new(Array.Empty<int>());

    public NodePath(params int[] segments)
    {
        _segments = segments ?? Array.Empty<int>();
    }

    public IReadOnlyList<int> Segments => _segments ?? Array.Empty<int>();

    public NodePath Append(int index)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var next = new int[Segments.Count + 1];
        for (var i = 0; i < Segments.Count; i++)
        {
            next[i] = Segments[i];
        }

        next[Segments.Count] = index;
        return new NodePath(next);
    }

    public override string ToString()
    {
        return Segments.Count == 0
            ? "/"
            : "/" + string.Join("/", Segments);
    }
}
