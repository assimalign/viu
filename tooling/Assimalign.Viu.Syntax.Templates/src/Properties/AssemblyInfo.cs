using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Assimalign.Viu.Syntax.Templates.Tests")]

// The shared .viu -> C# projection core is a build-time HOST of this compiler ([V01.01.06.11]); its
// component-usage validation ([SFC-USE-2]) asks the same DOM knowledge table the compiler itself uses to
// decide whether an undeclared attribute on a component is a plausible fallthrough attribute. Sharing
// the table is what keeps the two answers from drifting.
[assembly: InternalsVisibleTo("Assimalign.Viu.Compiler.SingleFileComponent")]
