using System.Runtime.CompilerServices;

// The library is deliberately all-internal ([V01.01.06.11]): the projection facade's result exposes the
// whole model graph, so a public facade would freeze ~14 evolving model types as public API. This IVT
// list IS the sanctioned-consumer declaration — the two build-time hosts and their test assemblies —
// matching the Assimalign.Viu.Compiler.Css -> generator precedent.
[assembly: InternalsVisibleTo("Assimalign.Viu.Generators.Syntax")]
[assembly: InternalsVisibleTo("Assimalign.Viu.LanguageService")]
[assembly: InternalsVisibleTo("Assimalign.Viu.Compiler.SingleFileComponent.Tests")]
[assembly: InternalsVisibleTo("Assimalign.Viu.Generators.Syntax.Tests")]
[assembly: InternalsVisibleTo("Assimalign.Viu.LanguageService.Tests")]
