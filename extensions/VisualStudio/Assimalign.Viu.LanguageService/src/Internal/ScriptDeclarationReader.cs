using System;
using System.Collections.Generic;

using Assimalign.Viu.Tooling.SingleFileComponent;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// Reads the members a component's <c>@script</c> block declares through the shared projection
/// core's <see cref="ScriptBlockAnalyzer.DescribeMembers"/> ([V01.01.06.11], #258) — the
/// completion- and outline-facing analogue of <c>@vue/compiler-sfc</c>'s <c>compileScript()</c>
/// binding scan ([V01.01.12.07.04], #261). The leading-using split, probe wrapper, and member
/// classification live once in <c>Assimalign.Viu.Tooling.SingleFileComponent</c>; this type
/// contributes only the service's bounded content-keyed cache, so an edit inside a template or
/// style block never pays a reparse.
/// </summary>
/// <remarks>
/// Not thread-safe: every call site runs inside the owning service's request lock, so the reader
/// assumes single-threaded use and takes no internal lock.
/// </remarks>
internal sealed class ScriptDeclarationReader
{
    // A document has at most two script blocks (@script + <script setup>), so this bound
    // comfortably covers interleaved requests across a few open documents while making growth
    // impossible.
    private const int MaximumCacheEntries = 8;

    private readonly Dictionary<string, IReadOnlyList<ScriptDeclaredMember>> cache =
        new(StringComparer.Ordinal);

    /// <summary>Gets how many <see cref="Read"/> calls missed the cache and parsed.</summary>
    internal int CacheMissCount { get; private set; }

    /// <summary>Reads the members declared by <paramref name="scriptContent"/>.</summary>
    /// <param name="scriptContent">The raw <c>@script</c> block content.</param>
    /// <returns>The declared fields, properties, and methods, in declaration order.</returns>
    internal IReadOnlyList<ScriptDeclaredMember> Read(string scriptContent)
    {
        if (cache.TryGetValue(scriptContent, out var cached))
        {
            return cached;
        }

        CacheMissCount++;
        // The shared analyzer reports block-content-relative member locations, so a description is
        // a pure function of the script text and the cache key stays the content string alone.
        IReadOnlyList<ScriptDeclaredMember> members =
            ScriptBlockAnalyzer.DescribeMembers(scriptContent);
        if (cache.Count == MaximumCacheEntries)
        {
            cache.Clear();
        }

        cache[scriptContent] = members;
        return members;
    }
}
