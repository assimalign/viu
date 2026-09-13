using System.Collections.Generic;

namespace Assimalign.Viu.Generators.FileRouting;

internal sealed class FileRoutingPage
{
    internal FileRoutingPage(FileRoutingInput input, string stem, string componentName, string[] segments)
    {
        Input = input;
        Stem = stem;
        ComponentName = componentName;
        Segments = segments;
    }

    internal FileRoutingInput Input { get; }
    internal string Stem { get; }
    internal string ComponentName { get; }
    internal string[] Segments { get; }
    internal string? PathOverride { get; set; }
    internal string? NameOverride { get; set; }
    internal string Path { get; set; } = string.Empty;
    internal string FullPath { get; set; } = string.Empty;
    internal string Name { get; set; } = string.Empty;
    internal bool ForwardParameters { get; set; }
    internal FileRoutingPage? Parent { get; set; }
    internal List<FileRoutingPage> Children { get; } = new();
}
