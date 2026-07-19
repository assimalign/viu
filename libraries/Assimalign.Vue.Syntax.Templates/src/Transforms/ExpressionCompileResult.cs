using System.Collections.Generic;

namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// The result of compiling a standalone expression through <see cref="TemplateExpressionCompiler"/>: the rewritten
/// C# <see cref="Code"/> and any <see cref="Diagnostics"/> the compile produced (a malformed expression is
/// reported here rather than thrown, matching the recoverable transform model).
/// </summary>
public sealed class ExpressionCompileResult
{
    /// <summary>Creates an expression compile result.</summary>
    /// <param name="code">The rewritten C# expression text (the original when a diagnostic prevented rewriting).</param>
    /// <param name="diagnostics">The diagnostics the compile produced.</param>
    public ExpressionCompileResult(string code, IReadOnlyList<CompilerError> diagnostics)
    {
        Code = code;
        Diagnostics = diagnostics;
    }

    /// <summary>The rewritten C# expression text.</summary>
    public string Code { get; }

    /// <summary>The diagnostics the compile produced (empty when the expression compiled cleanly).</summary>
    public IReadOnlyList<CompilerError> Diagnostics { get; }
}
