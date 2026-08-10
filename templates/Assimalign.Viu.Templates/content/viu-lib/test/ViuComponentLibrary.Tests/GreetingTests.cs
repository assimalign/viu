using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

using Shouldly;
using Xunit;

namespace ViuComponentLibrary.Tests;

public sealed class GreetingTests
{
    [Fact]
    public void GeneratedAssembly_Metadata_ContainsCompiledGreetingAndCatalog()
    {
        string assemblyPath = Path.Combine(
            System.AppContext.BaseDirectory,
            "ViuComponentLibrary.dll");
        using FileStream assemblyStream = File.OpenRead(assemblyPath);
        using PEReader portableExecutable = new(assemblyStream);
        MetadataReader metadata = portableExecutable.GetMetadataReader();

        string[] typeNames = metadata.TypeDefinitions
            .Select(handle => metadata.GetString(metadata.GetTypeDefinition(handle).Name))
            .ToArray();

        typeNames.ShouldContain("Greeting");
        typeNames.ShouldContain("GeneratedViuComponents");
    }
}
