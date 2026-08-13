using Assimalign.Viu.Syntax;
using Assimalign.Viu.Syntax.SingleFileComponent;

namespace Assimalign.Viu.LanguageService;

internal sealed record LanguageDocumentSyntax(
    LanguageDocumentFormat Format,
    SingleFileComponentTemplateBlock? Template,
    SingleFileComponentScriptBlock? Script,
    SingleFileComponentScriptBlock? ScriptSetup,
    SyntaxList<SingleFileComponentStyleBlock> Styles,
    SyntaxList<SingleFileComponentCustomBlock> CustomBlocks,
    SyntaxList<SingleFileComponentError> Errors);
