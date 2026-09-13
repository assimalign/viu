using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Assimalign.Viu.Generators.FileRouting;

internal static class FileRoutingTable
{
    internal static FileRoutingOutput Build(ImmutableArray<FileRoutingInput> files, FileRoutingOptions options, CancellationToken cancellationToken)
    {
        var diagnostics = new List<Diagnostic>();
        if (!options.Enabled)
        {
            return new FileRoutingOutput(null, diagnostics);
        }
        if (!options.IsValid)
        {
            diagnostics.Add(Diagnostic.Create(FileRoutingDiagnostics.InvalidConfiguration, Location.None));
            return new FileRoutingOutput(Emit(Array.Empty<FileRoutingPage>(), options.RootNamespace), diagnostics);
        }

        var orderedFiles = new List<FileRoutingInput>(files);
        orderedFiles.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Path, right.Path));
        var pages = new List<FileRoutingPage>();
        var identities = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var input in orderedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = input.Path.Substring(options.PagesRoot.Length);
            var stem = relative.Substring(0, relative.Length - 4);
            var names = stem.Split('/');
            var componentName = FileRoutingSegments.ComponentName(names[names.Length - 1]);
            if (identities.TryGetValue(componentName, out var other))
            {
                diagnostics.Add(FileRoutingDiagnostics.Create(FileRoutingDiagnostics.DuplicateComponent, input, componentName, other));
            }
            else
            {
                identities.Add(componentName, relative);
            }

            var segments = new string[names.Length];
            var valid = true;
            for (var index = 0; index < names.Length; index++)
            {
                if (index == names.Length - 1 && string.Equals(names[index], "Index", StringComparison.OrdinalIgnoreCase))
                {
                    segments[index] = string.Empty;
                }
                else if (!FileRoutingSegments.TryMap(names[index], out segments[index]))
                {
                    diagnostics.Add(FileRoutingDiagnostics.Create(FileRoutingDiagnostics.InvalidSegment, input, names[index]));
                    valid = false;
                }
            }
            if (!valid)
            {
                continue;
            }

            var page = new FileRoutingPage(input, stem, componentName, segments);
            if (!FileRoutingOverrides.TryRead(page, out var problem))
            {
                diagnostics.Add(FileRoutingDiagnostics.Create(FileRoutingDiagnostics.MalformedRoute, input, problem!));
                continue;
            }
            pages.Add(page);
        }

        var byStem = new Dictionary<string, FileRoutingPage>(StringComparer.Ordinal);
        foreach (var page in pages)
        {
            // .viu/.vue peers already produce a duplicate component diagnostic; keep the first
            // deterministic value here rather than throwing from the incremental pipeline.
            if (!byStem.ContainsKey(page.Stem))
            {
                byStem.Add(page.Stem, page);
            }
        }
        var roots = new List<FileRoutingPage>();
        foreach (var page in pages)
        {
            var directory = page.Stem;
            while (directory.LastIndexOf('/') is var slash && slash >= 0)
            {
                directory = directory.Substring(0, slash);
                if (byStem.TryGetValue(directory, out var parent))
                {
                    page.Parent = parent;
                    parent.Children.Add(page);
                    break;
                }
            }
            if (page.Parent is null)
            {
                roots.Add(page);
            }
        }

        foreach (var page in roots)
        {
            Resolve(page, diagnostics);
        }

        var paths = new Dictionary<string, List<FileRoutingPage>>(StringComparer.Ordinal);
        var namesByRoute = new Dictionary<string, FileRoutingPage>(StringComparer.Ordinal);
        foreach (var page in pages)
        {
            if (!paths.TryGetValue(page.FullPath, out var samePath))
            {
                samePath = new List<FileRoutingPage>();
                paths.Add(page.FullPath, samePath);
            }
            foreach (var candidate in samePath)
            {
                if (!IsDefaultChild(page, candidate) && !IsDefaultChild(candidate, page))
                {
                    diagnostics.Add(FileRoutingDiagnostics.Create(FileRoutingDiagnostics.DuplicatePath, page.Input, page.FullPath, candidate.Stem));
                    break;
                }
            }
            samePath.Add(page);
            if (namesByRoute.TryGetValue(page.Name, out var other))
            {
                diagnostics.Add(FileRoutingDiagnostics.Create(FileRoutingDiagnostics.DuplicateName, page.Input, page.Name, other.Stem));
            }
            else
            {
                namesByRoute.Add(page.Name, page);
            }
        }

        // Errors make the build fail. An empty generated body remains compilable and never creates a
        // partially valid runtime table should an editor inspect the generated source after recovery.
        return new FileRoutingOutput(Emit(diagnostics.Count == 0 ? roots : Array.Empty<FileRoutingPage>(), options.RootNamespace), diagnostics);
    }

    private static void Resolve(FileRoutingPage page, List<Diagnostic> diagnostics)
    {
        var skipped = page.Parent?.Segments.Length ?? 0;
        var relative = string.Join("/", page.Segments, skipped, page.Segments.Length - skipped).TrimEnd('/');
        page.Path = page.PathOverride ?? (page.Parent is null ? "/" + relative : relative);
        if (page.Parent is null && !page.Path.StartsWith("/", StringComparison.Ordinal))
        {
            diagnostics.Add(FileRoutingDiagnostics.Create(FileRoutingDiagnostics.MalformedRoute, page.Input, "top-level paths must begin with '/'"));
        }
        page.FullPath = page.Path.StartsWith("/", StringComparison.Ordinal) || page.Parent is null
            ? page.Path
            : page.Parent.FullPath.TrimEnd('/') + (page.Path.Length == 0 ? string.Empty : "/" + page.Path);
        if (page.FullPath.Length == 0)
        {
            page.FullPath = "/";
        }
        if (!FileRoutingSegments.TryValidatePath(page.FullPath, out var problem, out var catchAllNotLast))
        {
            diagnostics.Add(FileRoutingDiagnostics.Create(catchAllNotLast ? FileRoutingDiagnostics.CatchAllPosition : FileRoutingDiagnostics.MalformedRoute,
                page.Input, catchAllNotLast ? page.FullPath : problem!));
        }
        page.Name = page.NameOverride ?? (FileRoutingSegments.RouteName(page.FullPath)
            + (page.Parent is not null && page.Path.Length == 0 ? "-index" : string.Empty));
        page.ForwardParameters = page.FullPath.IndexOf(':') >= 0;
        foreach (var child in page.Children)
        {
            Resolve(child, diagnostics);
        }
    }

    private static bool IsDefaultChild(FileRoutingPage child, FileRoutingPage parent)
        => child.Parent == parent && child.Path.Length == 0;

    private static string Emit(IReadOnlyList<FileRoutingPage> pages, string rootNamespace)
    {
        var builder = new StringBuilder("// <auto-generated/>\n#nullable enable\n\n");
        if (rootNamespace.Length != 0)
        {
            builder.Append("namespace ").Append(rootNamespace).Append(";\n\n");
        }
        builder.Append("/// <summary>Creates the eager page route table using ordinary [RTR-1] route records.</summary>\n")
            .Append("public static class GeneratedViuFileRoutes\n{\n")
            .Append("    /// <summary>Creates fresh records; component activation uses the generated component catalog ([RTR-7], [RTR-11]).</summary>\n")
            .Append("    /// <returns>The deterministic page route table.</returns>\n")
            .Append("    public static global::System.Collections.Generic.IReadOnlyList<global::Assimalign.Viu.Router.RouteRecord> Create()\n")
            .Append("        => global::Assimalign.Viu.FileRouting.FileRoutes.Create(\n")
            .Append("            new global::Assimalign.Viu.FileRouting.FileRouteDescriptor[]\n            {\n");
        AppendPages(builder, pages, 4, 0);
        return builder.Append("            });\n}\n").ToString();
    }

    private static void AppendPages(StringBuilder builder, IReadOnlyList<FileRoutingPage> pages, int indentation, int depth)
    {
        var ordered = new List<FileRoutingPage>(pages);
        ordered.Sort(static (left, right) =>
        {
            var compared = StringComparer.Ordinal.Compare(left.Path, right.Path);
            return compared == 0 ? StringComparer.Ordinal.Compare(left.Name, right.Name) : compared;
        });
        foreach (var page in ordered)
        {
            var padding = new string(' ', indentation * 4);
            if (page.Children.Count != 0)
            {
                builder.Append(padding).Append("// [RTR-4] Layout ").Append(page.ComponentName)
                    .Append(" renders children with <RouterView :depth=\"").Append(depth + 1).Append("\"/>.\n");
            }
            builder.Append(padding).Append("new global::Assimalign.Viu.FileRouting.FileRouteDescriptor(")
                .Append(SymbolDisplay.FormatLiteral(page.Path, true)).Append(", ")
                .Append(SymbolDisplay.FormatLiteral(page.Name, true)).Append(", ")
                .Append(SymbolDisplay.FormatLiteral(page.ComponentName, true)).Append(", ")
                .Append(page.ForwardParameters ? "true" : "false");
            if (page.Children.Count != 0)
            {
                builder.Append(",\n").Append(padding).Append("    new global::Assimalign.Viu.FileRouting.FileRouteDescriptor[]\n")
                    .Append(padding).Append("    {\n");
                AppendPages(builder, page.Children, indentation + 2, depth + 1);
                builder.Append(padding).Append("    }");
            }
            builder.Append("),\n");
        }
    }
}
