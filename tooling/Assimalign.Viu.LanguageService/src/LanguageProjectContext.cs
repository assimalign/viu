using System.Collections.Generic;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// The editor-neutral, fully materialized project context the host resolves from restore
/// artifacts and feeds through
/// <see cref="IScriptSemanticLanguageService.ConfigureProjectContext"/>
/// ([V01.01.12.23], #259). The contract deliberately names no Roslyn or Language Server Protocol
/// types: reference assemblies are paths, sources are text, and the service decides what to build
/// from them.
/// </summary>
/// <param name="ProjectFilePath">The absolute path of the owning project file.</param>
/// <param name="ProjectDirectory">
/// The project directory exactly as the build publishes it (<c>build_property.ProjectDir</c>,
/// trailing separator included), so name and scope derivations agree with the source generator by
/// construction.
/// </param>
/// <param name="RootNamespace">The project's root namespace for generated component types.</param>
/// <param name="ReferenceAssemblyPaths">
/// The absolute paths of every compile-time reference assembly: package compile assets, framework
/// reference assemblies, and built outputs of referenced projects.
/// </param>
/// <param name="SourceDocuments">
/// The sibling source documents of the project, excluding the open document itself — its live
/// editor text wins over the disk copy.
/// </param>
/// <param name="PreprocessorSymbols">
/// The conditional-compilation symbols the build defines for this project (the target-framework
/// family — <c>NET</c>, <c>NETCOREAPP</c>, <c>NET10_0</c>, the <c>_OR_GREATER</c> chain — plus the
/// configuration's <c>DEBUG</c>/<c>TRACE</c>), so members guarded by <c>#if</c> compile into the
/// editor compilation exactly as the build sees them.
/// </param>
/// <param name="CacheStamp">
/// The host's opaque reuse hint for the restore artifacts and source cone. The service also compares
/// sibling text and each reference assembly's path, length, and write time, so an output replaced in
/// place invalidates derived state even when the host has not yet published a new stamp.
/// </param>
public sealed record LanguageProjectContext(
    string ProjectFilePath,
    string ProjectDirectory,
    string RootNamespace,
    IReadOnlyList<string> ReferenceAssemblyPaths,
    IReadOnlyList<LanguageProjectSourceDocument> SourceDocuments,
    IReadOnlyList<string> PreprocessorSymbols,
    string CacheStamp);
