using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Assimalign.Viu.Generators.Syntax")]
// The shared .viu -> C# projection core ([V01.01.06.11]) reuses SingleFileComponentPathComparison for
// name resolution and hot-reload identity, so path identity has one owner across every build-time host.
[assembly: InternalsVisibleTo("Assimalign.Viu.Compiler.SingleFileComponent")]
[assembly: InternalsVisibleTo("Assimalign.Viu.Compiler.Css.Tests")]
