namespace Assimalign.Vue.Compiler;

/// <summary>
/// Selects how the tokenizer treats special elements. The C# port of Vue 3.5's <c>ParseMode</c>
/// (<c>@vue/compiler-core</c> <c>tokenizer.ts</c>).
/// </summary>
public enum TemplateParseMode
{
    /// <summary>
    /// Platform-agnostic: every tag is treated the same, with no raw-text/RCDATA special casing
    /// (upstream <c>BASE</c>).
    /// </summary>
    Base = 0,

    /// <summary>
    /// HTML: adds the special parsing behaviour for <c>&lt;script&gt;</c>/<c>&lt;style&gt;</c> (raw text)
    /// and <c>&lt;title&gt;</c>/<c>&lt;textarea&gt;</c> (RCDATA) (upstream <c>HTML</c>).
    /// </summary>
    Html = 1,

    /// <summary>
    /// Single-file-component: content of all root-level tags except <c>&lt;template&gt;</c> is raw text
    /// (upstream <c>SFC</c>).
    /// </summary>
    Sfc = 2,
}
