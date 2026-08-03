using System;
using System.Collections.Generic;

using Assimalign.Viu.Tooling.SingleFileComponent;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// Reads the members a component's <c>@script</c> block declares through the shared projection
/// core's <see cref="ScriptBlockAnalyzer.DescribeMembers"/> ([V01.01.06.11], #258): the same member
/// scan the compiler runs, reused to feed completion and document outline so the editor can never
/// disagree with the build about what a component declares ([V01.01.12.07.04], #261).
/// The leading-using split, probe wrapper, and member
/// classification live once in <c>Assimalign.Viu.Tooling.SingleFileComponent</c>; this type
/// contributes only the service's bounded content-keyed cache, so an edit inside a template or
/// style block never pays a reparse.
/// </summary>
/// <remarks>
/// Thread-safe: language-service reads compute outside the service lock ([V01.01.12.23], #259), so
/// the reader guards its cache and miss counter with a private gate. Concurrent misses serialize
/// the underlying parse — bounded CPU work — which also keeps the clear-at-capacity policy from
/// thrashing under racing misses.
/// </remarks>
internal sealed class ScriptDeclarationReader
{
    // A document has at most two script blocks (@script + <script setup>), so this bound
    // comfortably covers interleaved requests across a few open documents while making growth
    // impossible.
    private const int MaximumCacheEntries = 8;

    private readonly object synchronization = new();
    private readonly Dictionary<string, IReadOnlyList<ScriptDeclaredMember>> cache =
        new(StringComparer.Ordinal);
    private int cacheMissCount;

    /// <summary>Gets how many <see cref="Read"/> calls missed the cache and parsed.</summary>
    internal int CacheMissCount
    {
        get
        {
            lock (synchronization)
            {
                return cacheMissCount;
            }
        }
    }

    /// <summary>Reads the members declared by <paramref name="scriptContent"/>.</summary>
    /// <param name="scriptContent">The raw <c>@script</c> block content.</param>
    /// <returns>The declared fields, properties, and methods, in declaration order.</returns>
    internal IReadOnlyList<ScriptDeclaredMember> Read(string scriptContent)
    {
        lock (synchronization)
        {
            if (cache.TryGetValue(scriptContent, out var cached))
            {
                return cached;
            }

            cacheMissCount++;
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
}
