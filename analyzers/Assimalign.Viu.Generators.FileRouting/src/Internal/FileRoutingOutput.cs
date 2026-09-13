using System.Collections.Generic;

using Microsoft.CodeAnalysis;

namespace Assimalign.Viu.Generators.FileRouting;

internal sealed class FileRoutingOutput
{
    internal FileRoutingOutput(string? source, IReadOnlyList<Diagnostic> diagnostics)
    {
        Source = source;
        Diagnostics = diagnostics;
    }

    internal string? Source { get; }
    internal IReadOnlyList<Diagnostic> Diagnostics { get; }
}
