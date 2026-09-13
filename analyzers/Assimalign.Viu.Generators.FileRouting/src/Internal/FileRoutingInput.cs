using System;

namespace Assimalign.Viu.Generators.FileRouting;

internal readonly struct FileRoutingInput : IEquatable<FileRoutingInput>
{
    internal FileRoutingInput(string path, string text)
    {
        Path = path;
        Text = text;
    }

    internal string Path { get; }
    internal string Text { get; }
    public bool Equals(FileRoutingInput other) => Path == other.Path && Text == other.Text;
    public override bool Equals(object? value) => value is FileRoutingInput other && Equals(other);
    public override int GetHashCode() => Path.GetHashCode() ^ Text.GetHashCode();
}
