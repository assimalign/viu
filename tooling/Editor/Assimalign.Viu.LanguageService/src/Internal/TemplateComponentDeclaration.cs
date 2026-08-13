using System.Collections.Generic;

using Assimalign.Viu.Compiler.SingleFileComponent;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// One component contract read from the same semantic compilation that backs projected C# editor
/// features. The compiler declaration preserves the exact [SFC-USE-5] identity and parameter surface;
/// events remain editor metadata because component-usage validation does not consume them.
/// </summary>
internal sealed record TemplateComponentDeclaration(
    ComponentDeclarationEntry CompilerDeclaration,
    IReadOnlyList<TemplateComponentEvent> Events);
