using System;
using System.IO;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Browser.Tests;

// [STA-11]: source-level contract for the WHATWG Web Storage boundary:
// https://html.spec.whatwg.org/multipage/webstorage.html#the-storage-interface.
public sealed class StateStorageJavaScriptContractTests
{
    [Fact]
    public void Read_UsesOneGetItemAndPreservesNullForMissingKeys()
    {
        string storage = ReadStorageExport();

        storage.ShouldContain(
            "read: (session, key) => (session ? window.sessionStorage : window.localStorage).getItem(key),");
        CountOccurrences(storage, ".getItem(").ShouldBe(1);
        storage.ShouldNotContain("JSON.parse");
        storage.ShouldNotContain("||");
        storage.ShouldNotContain("??");
    }

    [Fact]
    public void WriteAndRemove_UseOneWholeValueOperationAndPreserveNativeFailures()
    {
        string storage = ReadStorageExport();

        storage.ShouldContain(
            "write: (session, key, value) => (session ? window.sessionStorage : window.localStorage).setItem(key, value),");
        storage.ShouldContain(
            "remove: (session, key) => (session ? window.sessionStorage : window.localStorage).removeItem(key)");
        CountOccurrences(storage, ".setItem(").ShouldBe(1);
        CountOccurrences(storage, ".removeItem(").ShouldBe(1);
        storage.ShouldNotContain("catch");
        storage.ShouldNotContain("JSON.stringify");
        storage.ShouldNotContain("addEventListener");
    }

    private static int CountOccurrences(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;

    private static string ReadStorageExport()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string path = Path.Combine(directory.FullName, "src", "wwwroot", "viu-dom.js");
            if (File.Exists(path))
            {
                string source = File.ReadAllText(path);
                int start = source.IndexOf("export const stateStorage = {", StringComparison.Ordinal);
                start.ShouldBeGreaterThanOrEqualTo(0);
                int end = source.IndexOf('}', start);
                end.ShouldBeGreaterThan(start);
                return source[start..(end + 1)];
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate Browser's shipping viu-dom.js from the test output.");
    }
}
