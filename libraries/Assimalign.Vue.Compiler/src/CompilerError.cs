namespace Assimalign.Vue.Compiler;

/// <summary>
/// A parse error with its code, human-readable message, and source location. The C# port of Vue 3.5's
/// <c>CompilerError</c> (<c>@vue/compiler-core</c> <c>errors.ts</c>). The parser reports errors through
/// <see cref="ParserOptions.OnError"/> rather than throwing, matching Vue's recoverable-parsing model.
/// </summary>
/// <param name="Code">The error code.</param>
/// <param name="Message">The human-readable message for <paramref name="Code"/>.</param>
/// <param name="Location">The source location the error points at (a zero-width range at the offending offset).</param>
public sealed record CompilerError(CompilerErrorCode Code, string Message, SourceLocation Location);
