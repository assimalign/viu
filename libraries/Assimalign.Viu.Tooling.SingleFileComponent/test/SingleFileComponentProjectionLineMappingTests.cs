using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Assimalign.Viu.Tooling.Css;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Tooling.SingleFileComponent.Tests;

/// <summary>
/// Makes the library's core promise self-verifying at its own boundary ([V01.01.06.11] #258,
/// acceptance criterion 2): a build error inside <c>@script</c> points at the <c>.viu</c> file and
/// line. The projection API is driven directly (no generator host), its emitted scaffold is compiled,
/// and the semantic diagnostic's mapped <see cref="FileLinePositionSpan"/> must land on the
/// <c>.viu</c> coordinates through the emitted <c>#line</c> map — the same contract the generator
/// host pins end to end in
/// <c>SingleFileComponentScriptTests.ScriptTypeError_IsReportedOnTheViuFile_ViaLineDirective</c>
/// (analyzers/Assimalign.Viu.Generators.Syntax/test), with the two hosts' outputs held ordinal-equal
/// by <c>SingleFileComponentProjectionConformanceTests</c>.
/// </summary>
public sealed class SingleFileComponentProjectionLineMappingTests
{
    private const string ProjectDirectory = "C:/proj";
    private const string RootNamespace = "Demo";

    [Fact]
    public void Project_ScriptTypeError_MapsCompiledDiagnosticToTheViuFileLineAndColumn()
    {
        // A type error inside the script is a SEMANTIC error the projection's parse never sees, so it
        // surfaces only when the emitted C# is compiled. The #line map must resolve it to the .viu
        // file at the script member's own line (file line 2, zero-based line 1), never the .g.cs.
        const string source =
            "@script {\n" +                          // line 1
            "    public int Bad = \"not an int\";\n" + // line 2
            "}\n";                                    // line 3

        var input = CreateInput($"{ProjectDirectory}/Widget.viu", source);
        var result = SingleFileComponentProjection.Project(input, CancellationToken.None);

        result.Diagnostics.Count.ShouldBe(0); // syntactically valid C#, so the projection reports nothing
        var generated = SingleFileComponentSourceEmitter.Emit(result.Model);

        var conversion = Compile(generated)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ShouldHaveSingleItem();
        conversion.Id.ShouldBe("CS0029"); // cannot implicitly convert 'string' to 'int'
        // GetMappedLineSpan honors the emitted #line directive (GetLineSpan would give the physical
        // generated-file location); the mapped span is what an IDE/CLI build and the debugger resolve to.
        var span = conversion.Location.GetMappedLineSpan();
        span.Path.ShouldEndWith("Widget.viu");
        span.StartLinePosition.Line.ShouldBe(1);       // .viu file line 2, zero-based
        span.StartLinePosition.Character.ShouldBe(21); // the "not an int" literal column, preserved because
                                                       // the script is emitted flush at column 0 (no re-indent)
    }

    // The value-equatable source read the language service builds when it drives the projection: the
    // shared name resolver and scope-id derivation over the same two build properties the generator
    // reads (a script-only component, so the scaffold compiles against the base class library alone).
    private static SingleFileComponentProjectionInput CreateInput(string path, string content)
    {
        var names = SingleFileComponentNameResolver.Resolve(path, ProjectDirectory, RootNamespace);
        return new SingleFileComponentProjectionInput(
            SingleFileComponentFormat.Viu,
            path,
            path.Substring(path.LastIndexOf('/') + 1),
            content,
            names.Namespace,
            names.ClassName,
            names.HintName,
            StyleScopeId.Resolve(path, ProjectDirectory),
            HotReloadComponentIdentifier: null,
            HasCanonicalPeer: false);
    }

    // Compiles the emitted scaffold against the test host's platform assemblies so semantic
    // diagnostics — the ones the #line map must relocate — actually surface.
    private static ImmutableArray<Diagnostic> Compile(string generated)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.Length > 0)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));

        return CSharpCompilation.Create(
            "Assimalign.Viu.Tooling.SingleFileComponent.LineMappingTestAssembly",
            new[] { CSharpSyntaxTree.ParseText(generated, parseOptions) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable))
            .GetDiagnostics();
    }
}
