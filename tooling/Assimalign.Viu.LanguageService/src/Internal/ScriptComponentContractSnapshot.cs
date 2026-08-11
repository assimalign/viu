using System.Collections.Generic;

using Assimalign.Viu.Compiler.SingleFileComponent;

namespace Assimalign.Viu.LanguageService;

/// <summary>Component contracts and the live projection from which the current contract was read.</summary>
internal sealed record ScriptComponentContractSnapshot(
    IReadOnlyList<TemplateComponentDeclaration> Declarations,
    SingleFileComponentProjectionResult Projection);
