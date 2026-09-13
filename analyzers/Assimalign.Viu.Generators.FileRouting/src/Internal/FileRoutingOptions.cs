using System;
using System.Collections.Generic;

using Microsoft.CodeAnalysis.Diagnostics;

namespace Assimalign.Viu.Generators.FileRouting;

internal readonly struct FileRoutingOptions : IEquatable<FileRoutingOptions>
{
    internal FileRoutingOptions(bool enabled, string projectDirectory, string pagesDirectory, string rootNamespace)
    {
        Enabled = enabled;
        ProjectDirectory = NormalizePath(projectDirectory).TrimEnd('/');
        PagesDirectory = pagesDirectory.Replace('\\', '/');
        RootNamespace = rootNamespace;
        PagesRoot = NormalizePath(ProjectDirectory + "/" + PagesDirectory).TrimEnd('/') + "/";
    }

    internal bool Enabled { get; }
    internal string ProjectDirectory { get; }
    internal string PagesDirectory { get; }
    internal string RootNamespace { get; }
    internal string PagesRoot { get; }
    internal bool IsValid => ProjectDirectory.Length != 0
        && PagesDirectory.Length != 0 && !PagesDirectory.StartsWith("/", StringComparison.Ordinal)
        && PagesDirectory.IndexOf(':') < 0;

    internal bool Contains(string path) => IsValid && NormalizePath(path).StartsWith(PagesRoot, StringComparison.Ordinal);

    internal static FileRoutingOptions Read(AnalyzerConfigOptions options)
    {
        options.TryGetValue("build_property.ViuFileRoutingEnabled", out var enabled);
        options.TryGetValue("build_property.ViuFileRoutingPagesDirectory", out var pagesDirectory);
        options.TryGetValue("build_property.ProjectDir", out var projectDirectory);
        options.TryGetValue("build_property.RootNamespace", out var rootNamespace);
        return new FileRoutingOptions(string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase),
            projectDirectory ?? string.Empty, pagesDirectory ?? "Pages", rootNamespace ?? string.Empty);
    }

    internal static string NormalizePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        var parts = new List<string>();
        foreach (var part in normalized.Split('/'))
        {
            if (part.Length == 0 || part == ".")
            {
                continue;
            }
            if (part == ".." && parts.Count != 0 && parts[parts.Count - 1] != "..")
            {
                parts.RemoveAt(parts.Count - 1);
            }
            else
            {
                parts.Add(part);
            }
        }
        return (normalized.StartsWith("/", StringComparison.Ordinal) ? "/" : string.Empty) + string.Join("/", parts);
    }

    public bool Equals(FileRoutingOptions other) => Enabled == other.Enabled
        && ProjectDirectory == other.ProjectDirectory && PagesDirectory == other.PagesDirectory
        && RootNamespace == other.RootNamespace;
    public override bool Equals(object? value) => value is FileRoutingOptions other && Equals(other);
    public override int GetHashCode() => ProjectDirectory.GetHashCode() ^ PagesDirectory.GetHashCode()
        ^ RootNamespace.GetHashCode() ^ Enabled.GetHashCode();
}
