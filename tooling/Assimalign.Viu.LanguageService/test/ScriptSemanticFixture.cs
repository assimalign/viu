using System;
using System.Collections.Generic;
using System.IO;

namespace Assimalign.Viu.LanguageService.Tests;

/// <summary>
/// Shared in-memory <see cref="LanguageProjectContext"/> fixtures for the semantic engine tests
/// ([V01.01.12.23], #259). References come from the test runtime's own assemblies — the running
/// machine's framework copies named by the trusted-platform-assemblies list plus the real
/// Assimalign.Viu.Components contract assembly — the spec's sanctioned test approach, so a member
/// typed from a metadata reference binds against genuine metadata rather than a stub.
/// </summary>
internal static class ScriptSemanticFixture
{
    internal const string ProjectFilePath = "C:/workspace/App/App.csproj";
    internal const string ProjectDirectory = "C:/workspace/App/";
    internal const string RootNamespace = "Test.App";
    internal const string DocumentFilePath = "C:\\workspace\\App\\AppShell.viu";
    internal const string DocumentUri = "file:///C:/workspace/App/AppShell.viu";

    internal static LanguageProjectContext CreateContext(
        params LanguageProjectSourceDocument[] sourceDocuments)
        => CreateContext("stamp-1", sourceDocuments);

    internal static LanguageProjectContext CreateContext(
        string cacheStamp,
        params LanguageProjectSourceDocument[] sourceDocuments)
        => CreateContext(cacheStamp, PreprocessorSymbols, sourceDocuments);

    internal static LanguageProjectContext CreateContext(
        string cacheStamp,
        IReadOnlyList<string> preprocessorSymbols,
        params LanguageProjectSourceDocument[] sourceDocuments)
        => new(
            ProjectFilePath,
            ProjectDirectory,
            RootNamespace,
            ReferenceAssemblyPaths(),
            sourceDocuments,
            preprocessorSymbols,
            cacheStamp);

    internal static readonly IReadOnlyList<string> PreprocessorSymbols =
        ["TRACE", "DEBUG", "NET", "NETCOREAPP", "NET10_0", "NET10_0_OR_GREATER"];

    internal static IReadOnlyList<string> ReferenceAssemblyPaths()
    {
        var paths = new List<string>();
        var frameworkFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "System.Private.CoreLib.dll",
            "System.Runtime.dll",
            "netstandard.dll",
            "System.Collections.dll",
            "System.Linq.dll",
            "System.Threading.dll",
            "System.Threading.Tasks.dll",
        };
        var trustedAssemblies =
            AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty;
        foreach (var path in trustedAssemblies.Split(Path.PathSeparator))
        {
            if (frameworkFileNames.Contains(Path.GetFileName(path)))
            {
                paths.Add(path);
            }
        }

        paths.Add(typeof(Assimalign.Viu.Components.ComponentContext).Assembly.Location);
        return paths;
    }
}
