using System.Collections.Generic;

namespace Assimalign.Viu.LanguageService;

internal static class ViuCompletionCatalog
{
    // The hybrid .viu container ([V01.01.06.10]): <template> and <style> are tags whose options are
    // tag attributes, while the C# script block keeps the @-block grammar. The legacy @template and
    // @style containers still parse during the migration window but are no longer offered.
    internal static IReadOnlyList<LanguageCompletionItem> BlockHeaders { get; } =
    [
        Snippet(
            "template",
            "Viu template block",
            "Contains the component's template markup, compiled to a render function at build time.",
            "<template>\n\t$0\n</template>",
            "01"),
        Snippet(
            "@script",
            "Viu C# script block",
            "Contains members merged into the generated partial component class.",
            "@script {\n\t$0\n}",
            "02"),
        Snippet(
            "style",
            "Viu CSS style block",
            "Contains component CSS; `scoped`, `module`, and `lang` are tag attributes.",
            "<style>\n\t$0\n</style>",
            "03"),
        Snippet(
            "style scoped",
            "Scoped Viu CSS style block",
            "Contains component CSS scoped to this component.",
            "<style scoped>\n\t$0\n</style>",
            "04"),
        Snippet(
            "style module",
            "Viu CSS module style block",
            "Compiles class names as a CSS module exposed through the template binding.",
            "<style module>\n\t$0\n</style>",
            "05"),
    ];

    internal static IReadOnlyList<LanguageCompletionItem> VueBlockHeaders { get; } =
    [
        Snippet(
            "template",
            "Vue-format template block",
            "Contains the component's template markup, compiled to a render function at build time.",
            "<template>\n\t$0\n</template>",
            "01"),
        Snippet(
            "script",
            "Viu C# script block",
            "Contains C# members merged into the generated partial component class. The explicit csharp language is required.",
            "<script lang=\"csharp\">\n\t$0\n</script>",
            "02"),
        Snippet(
            "script setup",
            "Viu C# setup script block",
            "Contains C# members merged into the generated partial component class alongside an optional ordinary script.",
            "<script setup lang=\"csharp\">\n\t$0\n</script>",
            "03"),
        Snippet(
            "style",
            "Vue-format CSS style block",
            "Contains component CSS and supports scoped, module, and CSS language attributes.",
            "<style>\n\t$0\n</style>",
            "04"),
    ];

    internal static IReadOnlyList<LanguageCompletionItem> TemplateHeaderOptions { get; } =
    [
        Text("lang=\"html\"", LanguageCompletionItemKind.Property, "Template language", "Selects HTML template syntax.", "01"),
    ];

    internal static IReadOnlyList<LanguageCompletionItem> ScriptHeaderOptions { get; } =
    [
        Text("lang=\"csharp\"", LanguageCompletionItemKind.Property, "Script language", "Selects the C# script language.", "01"),
    ];

    internal static IReadOnlyList<LanguageCompletionItem> VueScriptHeaderOptions { get; } =
    [
        Text("lang=\"csharp\"", LanguageCompletionItemKind.Property, "Script language", "Selects the C# script language.", "01"),
        Text("setup", LanguageCompletionItemKind.Keyword, "Setup script", "Selects the second C# script slot and merges its members into the generated partial component.", "02"),
    ];

    internal static IReadOnlyList<LanguageCompletionItem> StyleHeaderOptions { get; } =
    [
        Text("scoped", LanguageCompletionItemKind.Keyword, "Scoped CSS", "Scopes the style block to this component.", "01"),
        Text("module", LanguageCompletionItemKind.Keyword, "CSS module", "Compiles class names as a CSS module.", "02"),
        Text("module=\"$style\"", LanguageCompletionItemKind.Property, "Named CSS module", "Exposes the module through the selected template binding.", "03"),
        Text("lang=\"css\"", LanguageCompletionItemKind.Property, "Style language", "Selects CSS syntax.", "04"),
    ];

    internal static IReadOnlyList<LanguageCompletionItem> TemplateDirectives { get; } =
    [
        Attribute("v-if", "Conditional directive", "Renders the element when the expression is truthy.", "v-if=\"$1\"", "01"),
        Attribute("v-else-if", "Conditional directive", "Adds another conditional branch.", "v-else-if=\"$1\"", "02"),
        Text("v-else", LanguageCompletionItemKind.Keyword, "Conditional directive", "Adds an unconditional alternative branch.", "03"),
        Attribute("v-for", "List directive", "Repeats the element for each value in a source.", "v-for=\"$1\"", "04"),
        Attribute("v-show", "Visibility directive", "Toggles element visibility while preserving the element.", "v-show=\"$1\"", "05"),
        Attribute("v-model", "Two-way binding", "Creates a form value binding.", "v-model=\"$1\"", "06"),
        Attribute("v-bind", "Property binding", "Binds an element property or component argument.", "v-bind:", "07"),
        Attribute("v-on", "Event listener", "Registers an event handler.", "v-on:", "08"),
        Attribute("v-slot", "Named slot", "Declares slot content and optional slot arguments.", "v-slot=\"$1\"", "09"),
        Text("v-once", LanguageCompletionItemKind.Keyword, "Render directive", "Renders the subtree once.", "10"),
        Attribute("v-memo", "Render memoization", "Skips updates while the dependency list is unchanged.", "v-memo=\"$1\"", "11"),
    ];

    internal static IReadOnlyList<LanguageCompletionItem> TemplateEvents { get; } =
    [
        Attribute("@click", "DOM event", "Registers a click handler.", "@click=\"$1\"", "01"),
        Attribute("@submit", "DOM event", "Registers a submit handler.", "@submit=\"$1\"", "02"),
        Attribute("@input", "DOM event", "Registers an input handler.", "@input=\"$1\"", "03"),
        Attribute("@change", "DOM event", "Registers a change handler.", "@change=\"$1\"", "04"),
        Attribute("@keydown", "DOM event", "Registers a keydown handler.", "@keydown=\"$1\"", "05"),
        Attribute("@keyup", "DOM event", "Registers a keyup handler.", "@keyup=\"$1\"", "06"),
    ];

    internal static IReadOnlyList<LanguageCompletionItem> TemplateBindings { get; } =
    [
        Attribute(":class", "Property binding", "Binds the element's class value.", ":class=\"$1\"", "01"),
        Attribute(":style", "Property binding", "Binds the element's inline style value.", ":style=\"$1\"", "02"),
        Attribute(":key", "Renderer identity", "Supplies a stable identity for keyed diffing.", ":key=\"$1\"", "03"),
        Attribute(":ref", "Template reference", "Captures the element or component reference.", ":ref=\"$1\"", "04"),
        Attribute(":disabled", "Boolean property binding", "Binds the disabled property.", ":disabled=\"$1\"", "05"),
        Attribute(":value", "Value binding", "Binds the element value.", ":value=\"$1\"", "06"),
    ];

    /// <summary>
    /// The tags Viu owns: its framework elements and its built-in components. The native elements are
    /// not listed here — they are enumerated from the shared DOM table, which is the same one the
    /// compiler classifies against, so the two can never disagree about what an element is.
    /// </summary>
    internal static IReadOnlyList<LanguageCompletionItem> TemplateTags { get; } =
    [
        Tag(
            "template",
            LanguageCompletionItemKind.Property,
            "Viu framework element",
            "Groups children without rendering an element of its own.",
            "20:template"),
        Tag(
            "slot",
            LanguageCompletionItemKind.Property,
            "Viu framework element",
            "Renders content the parent supplied for this outlet.",
            "20:slot"),
        Tag(
            "component",
            LanguageCompletionItemKind.Property,
            "Viu framework element",
            "Renders the component its bound `is` value selects.",
            "20:component"),
        Tag(
            "Transition",
            LanguageCompletionItemKind.Class,
            "Viu built-in component",
            "Animates a single child as it enters and leaves.",
            "10:Transition"),
        Tag(
            "TransitionGroup",
            LanguageCompletionItemKind.Class,
            "Viu built-in component",
            "Animates a list of children as items enter, leave, and move.",
            "10:TransitionGroup"),
        Tag(
            "KeepAlive",
            LanguageCompletionItemKind.Class,
            "Viu built-in component",
            "Retains a swapped-out child's state instead of unmounting it.",
            "10:KeepAlive"),
        Tag(
            "Suspense",
            LanguageCompletionItemKind.Class,
            "Viu built-in component",
            "Renders fallback content until its asynchronous children resolve.",
            "10:Suspense"),
    ];

    internal static IReadOnlyList<LanguageCompletionItem> ScriptGeneral { get; } =
    [
        Text("Context", LanguageCompletionItemKind.Property, "ComponentContext", "The generated component's current setup context.", "01"),
        Text("Reactive", LanguageCompletionItemKind.Class, "Reactive API", "The Viu reactivity facade.", "02"),
        Snippet(
            "reactive reference",
            "Reference<T> field",
            "Declares a reactive value used by the template.",
            "public Reference<$1> $2 { get; } = Reactive.Reference($3);",
            "03"),
        Snippet(
            "computed value",
            "Computed<T> field",
            "Declares a lazy cached value derived from reactive state.",
            "public Computed<$1> $2 { get; } = Reactive.Computed(() => $3);",
            "04"),
        Snippet(
            "mounted callback",
            "Lifecycle registration",
            "Registers asynchronous work for the mounted lifecycle. " +
                "`Context.Lifecycle.OnMounted($1);` registers exactly the same callback and stays " +
                "valid; the root form is the shorter idiom every component inherits (`[CMP-32]`).",
            "OnMounted($1);",
            "05"),
        Snippet(
            "async event handler",
            "Task-returning method",
            "Declares an asynchronous template event handler.",
            "public async Task $1()\n{\n\t$0\n}",
            "06"),
        // The generator scaffold's only other author-facing member. The render method and stable
        // render-cache-size provider are compiler-owned and must never be offered
        // (SingleFileComponentSourceEmitter, [SFC-CG-9]).
        Snippet(
            "OnSetup",
            "partial void OnSetup()",
            "Implements the generated setup hook. Runs once per mount after `Context` is assigned.",
            "partial void OnSetup()\n{\n\t$0\n}",
            "07"),
    ];

    /// <summary>
    /// The root-level lifecycle registration methods every component inherits from
    /// <c>ComponentTemplateBase</c> (<c>[CMP-32]</c>).
    /// </summary>
    /// <remarks>
    /// These are ordinary inherited members, so the semantic engine already offers them from the
    /// real compilation and its bound items win the first-wins label dedup. They are listed here so
    /// the degraded, syntax-only answer — a document with no restored project state — still knows
    /// the idiomatic authoring surface rather than only the <c>Context.Lifecycle</c> form.
    /// </remarks>
    internal static IReadOnlyList<LanguageCompletionItem> RootLifecycleRegistrations { get; } =
    [
        Lifecycle(
            "OnBeforeMount",
            "Action | Func<Task> | Func<CancellationToken, Task>",
            "runs before the initial subtree is mounted"),
        Lifecycle(
            "OnMounted",
            "Action | Func<Task> | Func<CancellationToken, Task>",
            "runs after the initial subtree is mounted"),
        Lifecycle(
            "OnBeforeUpdate",
            "Action | Func<Task> | Func<CancellationToken, Task>",
            "runs before a later subtree is patched"),
        Lifecycle(
            "OnUpdated",
            "Action | Func<Task> | Func<CancellationToken, Task>",
            "runs after a later subtree is patched"),
        Lifecycle(
            "OnBeforeUnmount",
            "Action | Func<Task> | Func<CancellationToken, Task>",
            "runs before the component is unmounted"),
        Lifecycle(
            "OnUnmounted",
            "Action | Func<Task> | Func<CancellationToken, Task>",
            "runs after the component is unmounted"),
        Lifecycle(
            "OnErrorCaptured",
            "Func<Exception, ComponentContext?, string, bool>",
            "captures an error from a descendant, returning false to stop propagation"),
        Lifecycle(
            "OnServerPrefetch",
            "Func<Task> | Func<CancellationToken, Task>",
            "is awaited by server-side rendering before the component is serialized"),
        Lifecycle(
            "OnActivated",
            "Action | Func<Task> | Func<CancellationToken, Task>",
            "runs when a cached subtree is reactivated"),
        Lifecycle(
            "OnDeactivated",
            "Action | Func<Task> | Func<CancellationToken, Task>",
            "runs when a cached subtree is deactivated"),
    ];

    /// <summary>
    /// C# language keywords offered inside an <c>@script</c> block.
    /// </summary>
    /// <remarks>
    /// This is a lexical aid, not C# IntelliSense: the language service hosts no compilation, so it
    /// knows nothing about types, members, or namespaces in scope. Keywords are offered because the
    /// alternative — answering a partially typed <c>using</c> with a Viu snippet list — is actively
    /// misleading. Declared-member completion ([V01.01.12.07.04], #261,
    /// <see cref="ScriptDeclarationReader"/>) ranks this file's own <c>@script</c> declarations
    /// above these keywords; the remaining gap is workspace-backed semantics, tracked separately
    /// ([V01.01.12.23], #259).
    /// </remarks>
    internal static IReadOnlyList<LanguageCompletionItem> ScriptKeywords { get; } =
        CreateScriptKeywords();

    private static IReadOnlyList<LanguageCompletionItem> CreateScriptKeywords()
    {
        string[] keywords =
        [
            "abstract", "as", "async", "await", "base", "bool", "break", "byte", "case", "catch",
            "char", "checked", "class", "const", "continue", "decimal", "default", "delegate", "do",
            "double", "else", "enum", "event", "explicit", "extern", "false", "finally", "fixed",
            "float", "for", "foreach", "get", "global", "goto", "if", "implicit", "in", "init", "int",
            "interface", "internal", "is", "lock", "long", "nameof", "namespace", "new", "nint",
            "not", "null", "object", "operator", "out", "override", "params", "private", "protected",
            "public", "readonly", "record", "ref", "required", "return", "sbyte", "sealed", "set",
            "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw",
            "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using",
            "var", "virtual", "void", "volatile", "when", "where", "while", "with", "yield",
        ];

        var items = new LanguageCompletionItem[keywords.Length];
        for (var index = 0; index < keywords.Length; index++)
        {
            items[index] = Text(
                keywords[index],
                LanguageCompletionItemKind.Keyword,
                "C# keyword",
                $"The C# `{keywords[index]}` keyword.",
                "90");
        }

        return items;
    }

    internal static IReadOnlyList<LanguageCompletionItem> ContextMembers { get; } =
    [
        Member("Bindings", "ComponentBindings", "Gets the contract-resolved parent invocation.", "01"),
        Member("Services", "IServiceProvider?", "Gets the application service resolver.", "02"),
        Member("Lifecycle", "ComponentLifecycle", "Gets lifecycle registration and component cancellation.", "03"),
        Member("Scope", "IReactiveEffectScope", "Gets the reactive scope that owns this component.", "04"),
        Member("WatchScheduler", "IReactiveWatchScheduler?", "Gets the component watch scheduler.", "05"),
        Member("Parent", "ComponentContext?", "Gets the parent component context.", "06"),
        Method("Emit", "void Emit(string name, params object?[] arguments)", "Emits a component event to the parent.", "07"),
        Method("Expose", "void Expose(object? value)", "Selects the public template-reference surface.", "08"),
        Method("Warn", "void Warn(string message)", "Routes a development warning through the application.", "09"),
        Method("Watch", "WatchHandle Watch<TValue>(Func<TValue> getter, Action<TValue, TValue> callback)", "Watches a getter within this component's reactive scope.", "10"),
    ];

    internal static IReadOnlyList<LanguageCompletionItem> ReactiveMembers { get; } =
    [
        Method("Reference", "Reference<T> Reference<T>(T value)", "Creates a deeply reactive reference.", "01"),
        Method("ShallowReference", "ShallowReference<T> ShallowReference<T>(T value)", "Creates a shallow reactive reference.", "02"),
        Method("Computed", "Computed<T> Computed<T>(Func<T> getter)", "Creates a lazy cached computed value.", "03"),
        Method("Effect", "ReactiveEffect Effect(Action action)", "Runs and tracks a reactive effect.", "04"),
        Method("EffectScope", "EffectScope EffectScope(bool detached = false)", "Creates a disposable reactive scope.", "05"),
        Method("Watch", "WatchHandle Watch(...)", "Observes a reactive source.", "06"),
        Method("WatchEffect", "WatchHandle WatchEffect(Action effect)", "Runs an automatically tracked watcher.", "07"),
        Method("OnScopeDispose", "void OnScopeDispose(Action callback)", "Registers cleanup with the active scope.", "08"),
        Method("Batch", "IDisposable Batch()", "Opens an exception-safe reactive notification batch.", "09"),
    ];

    internal static IReadOnlyList<LanguageCompletionItem> StyleProperties { get; } =
    [
        CssProperty("display", "Controls the element's display layout.", "01"),
        CssProperty("color", "Sets the foreground color.", "02"),
        CssProperty("background", "Sets background layers.", "03"),
        CssProperty("font-size", "Sets the font size.", "04"),
        CssProperty("margin", "Sets outer spacing.", "05"),
        CssProperty("padding", "Sets inner spacing.", "06"),
        CssProperty("width", "Sets the element width.", "07"),
        CssProperty("height", "Sets the element height.", "08"),
        CssProperty("gap", "Sets grid or flex spacing.", "09"),
        CssProperty("grid-template-columns", "Defines grid columns.", "10"),
        CssProperty("align-items", "Aligns flex or grid children.", "11"),
        CssProperty("justify-content", "Distributes flex or grid children.", "12"),
    ];

    private static LanguageCompletionItem Snippet(
        string label,
        string detail,
        string documentation,
        string insertText,
        string sortText)
        => new(label, LanguageCompletionItemKind.Snippet, detail, documentation, insertText, true, sortText);

    /// <summary>
    /// Creates an attribute completion, which commits up to the equals sign.
    /// </summary>
    /// <param name="label">The attribute name, its shorthand included.</param>
    /// <param name="detail">The category shown beside the name.</param>
    /// <param name="documentation">What the attribute does.</param>
    /// <param name="insertText">The committed text, ending at the equals sign.</param>
    /// <param name="sortText">The stable key used to order the item.</param>
    /// <returns>The completion item.</returns>
    /// <remarks>
    /// The item writes its own empty value, because the value is where the author is going. A client
    /// that expands snippets puts the caret in it from the tabstop; a client that does not receives
    /// the tabstop rendered out and Visual Studio's commit adapter places the caret instead.
    /// </remarks>
    private static LanguageCompletionItem Attribute(
        string label,
        string detail,
        string documentation,
        string insertText,
        string sortText)
        => new(
            label,
            LanguageCompletionItemKind.Property,
            detail,
            documentation,
            insertText,
            // The flag states whether the text carries placeholders, so it is read off the text
            // rather than repeated at every call site where the two could drift apart.
            IsSnippet: insertText.IndexOf('$') >= 0,
            sortText);

    private static LanguageCompletionItem Text(
        string label,
        LanguageCompletionItemKind kind,
        string detail,
        string documentation,
        string sortText)
        => new(label, kind, detail, documentation, label, false, sortText);

    /// <summary>
    /// Creates an element completion, which commits the open tag alone.
    /// </summary>
    /// <param name="label">The element name.</param>
    /// <param name="kind">The glyph category: a component is a type, a framework element is not.</param>
    /// <param name="detail">The category shown beside the name.</param>
    /// <param name="documentation">What the element is.</param>
    /// <param name="sortText">The stable key used to order the item.</param>
    /// <returns>The completion item.</returns>
    /// <remarks>
    /// Committing writes <c>&lt;template</c> and stops, leaving the author exactly where typing the
    /// name by hand would have left them — and the <c>&gt;</c> they type next is what closes the
    /// element and puts the caret between the two tags. Writing the whole element here instead would
    /// need the caret placed inside it, which a completion item cannot ask for unless the editor
    /// expands snippets, and the two spellings of the same element would then behave differently.
    /// </remarks>
    private static LanguageCompletionItem Tag(
        string label,
        LanguageCompletionItemKind kind,
        string detail,
        string documentation,
        string sortText)
        => new(
            label,
            kind,
            detail,
            documentation,
            "<" + label,
            IsSnippet: false,
            sortText);

    private static LanguageCompletionItem Member(
        string label,
        string detail,
        string documentation,
        string sortText)
        => Text(label, LanguageCompletionItemKind.Property, detail, documentation, sortText);

    private static LanguageCompletionItem Method(
        string label,
        string detail,
        string documentation,
        string sortText)
        => Text(label, LanguageCompletionItemKind.Method, detail, documentation, sortText);

    // One root-level lifecycle registration. Every entry shares the sort band "08": they rank below
    // the scaffold items an author reaches for first and above the keyword catalog's "90".
    private static LanguageCompletionItem Lifecycle(
        string label,
        string callbackTypes,
        string behavior)
        => Method(
            label,
            $"void {label}({callbackTypes} callback)",
            $"Registers a callback that {behavior} — the root-level equivalent of " +
                $"`Context.Lifecycle.{label}`, inherited by every component. Specified by `[CMP-32]`.",
            "08");

    private static LanguageCompletionItem CssProperty(
        string label,
        string documentation,
        string sortText)
        => Snippet(label, "CSS property", documentation, $"{label}: $1;", sortText);
}
