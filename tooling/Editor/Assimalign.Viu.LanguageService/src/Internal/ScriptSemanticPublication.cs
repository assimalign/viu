using System.Collections.Generic;

using Assimalign.Viu.Compiler.SingleFileComponent;

namespace Assimalign.Viu.LanguageService;

/// <summary>Semantic layers computed from one immutable live-document compilation fork.</summary>
internal sealed record ScriptSemanticPublication(
    IReadOnlyList<LanguageClassification> Classifications,
    IReadOnlyList<LanguageDiagnostic> Diagnostics,
    IReadOnlyList<TemplateComponentDeclaration>? ComponentContracts,
    SingleFileComponentProjectionResult Projection);
