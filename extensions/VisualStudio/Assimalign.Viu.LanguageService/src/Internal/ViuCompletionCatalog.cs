using System.Collections.Generic;

namespace Assimalign.Viu.LanguageService;

internal static class ViuCompletionCatalog
{
    internal static IReadOnlyList<LanguageCompletionItem> BlockHeaders { get; } =
    [
        Snippet(
            "@template",
            "Viu template block",
            "Contains the component's Vue-compatible template markup.",
            "@template {\n\t$0\n}",
            "01"),
        Snippet(
            "@script",
            "Viu C# script block",
            "Contains members merged into the generated partial component class.",
            "@script {\n\t$0\n}",
            "02"),
        Snippet(
            "@style",
            "Viu CSS style block",
            "Contains component CSS and supports `scoped`, `module`, and `lang` options.",
            "@style {\n\t$0\n}",
            "03"),
    ];

    internal static IReadOnlyList<LanguageCompletionItem> TemplateHeaderOptions { get; } =
    [
        Text("lang=\"html\"", LanguageCompletionItemKind.Property, "Template language", "Selects HTML template syntax.", "01"),
    ];

    internal static IReadOnlyList<LanguageCompletionItem> ScriptHeaderOptions { get; } =
    [
        Text("lang=\"csharp\"", LanguageCompletionItemKind.Property, "Script language", "Selects the C# script language.", "01"),
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
        Snippet("v-if", "Conditional directive", "Renders the element when the expression is truthy.", "v-if=\"$1\"", "01"),
        Snippet("v-else-if", "Conditional directive", "Adds another conditional branch.", "v-else-if=\"$1\"", "02"),
        Text("v-else", LanguageCompletionItemKind.Keyword, "Conditional directive", "Adds an unconditional alternative branch.", "03"),
        Snippet("v-for", "List directive", "Repeats the element for each value in a source.", "v-for=\"$1 in $2\"", "04"),
        Snippet("v-show", "Visibility directive", "Toggles element visibility while preserving the element.", "v-show=\"$1\"", "05"),
        Snippet("v-model", "Two-way binding", "Creates a form value binding.", "v-model=\"$1\"", "06"),
        Snippet("v-bind", "Property binding", "Binds an element property or component argument.", ":$1=\"$2\"", "07"),
        Snippet("v-on", "Event listener", "Registers an event handler.", "@$1=\"$2\"", "08"),
        Snippet("v-slot", "Named slot", "Declares slot content and optional slot arguments.", "#$1=\"$2\"", "09"),
        Text("v-once", LanguageCompletionItemKind.Keyword, "Render directive", "Renders the subtree once.", "10"),
        Snippet("v-memo", "Render memoization", "Skips updates while the dependency list is unchanged.", "v-memo=\"[$1]\"", "11"),
    ];

    internal static IReadOnlyList<LanguageCompletionItem> TemplateEvents { get; } =
    [
        Snippet("@click", "DOM event", "Registers a click handler.", "@click=\"$1\"", "01"),
        Snippet("@submit", "DOM event", "Registers a submit handler.", "@submit=\"$1\"", "02"),
        Snippet("@input", "DOM event", "Registers an input handler.", "@input=\"$1\"", "03"),
        Snippet("@change", "DOM event", "Registers a change handler.", "@change=\"$1\"", "04"),
        Snippet("@keydown", "DOM event", "Registers a keydown handler.", "@keydown=\"$1\"", "05"),
        Snippet("@keyup", "DOM event", "Registers a keyup handler.", "@keyup=\"$1\"", "06"),
    ];

    internal static IReadOnlyList<LanguageCompletionItem> TemplateBindings { get; } =
    [
        Snippet(":class", "Property binding", "Binds the element's class value.", ":class=\"$1\"", "01"),
        Snippet(":style", "Property binding", "Binds the element's inline style value.", ":style=\"$1\"", "02"),
        Snippet(":key", "Renderer identity", "Supplies a stable identity for keyed diffing.", ":key=\"$1\"", "03"),
        Snippet(":ref", "Template reference", "Captures the element or component reference.", ":ref=\"$1\"", "04"),
        Snippet(":disabled", "Boolean property binding", "Binds the disabled property.", ":disabled=\"$1\"", "05"),
        Snippet(":value", "Value binding", "Binds the element value.", ":value=\"$1\"", "06"),
    ];

    internal static IReadOnlyList<LanguageCompletionItem> TemplateTags { get; } =
    [
        Tag("div", "Container element", "01"),
        Tag("span", "Inline container element", "02"),
        Tag("button", "Button element", "03"),
        Tag("input", "Input element", "04", selfClosing: true),
        Tag("form", "Form element", "05"),
        Tag("label", "Label element", "06"),
        Tag("ul", "Unordered list element", "07"),
        Tag("li", "List item element", "08"),
        Tag("p", "Paragraph element", "09"),
        Tag("section", "Section element", "10"),
        Tag("template", "Non-rendering template fragment", "11"),
        Tag("component", "Dynamic component", "12"),
        Tag("slot", "Slot outlet", "13", selfClosing: true),
        Tag("Transition", "Viu transition component", "14"),
        Tag("TransitionGroup", "Viu transition-group component", "15"),
        Tag("KeepAlive", "Viu keep-alive component", "16"),
        Tag("Suspense", "Viu suspense component", "17"),
    ];

    internal static IReadOnlyList<LanguageCompletionItem> ScriptGeneral { get; } =
    [
        Text("Context", LanguageCompletionItemKind.Property, "IComponentContext", "The generated component's current setup context.", "01"),
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
            "Registers asynchronous work for the mounted lifecycle.",
            "Context.Lifecycle.OnMounted($1);",
            "05"),
        Snippet(
            "async event handler",
            "Task-returning method",
            "Declares an asynchronous template event handler.",
            "public async Task $1()\n{\n\t$0\n}",
            "06"),
    ];

    internal static IReadOnlyList<LanguageCompletionItem> ContextMembers { get; } =
    [
        Member("Arguments", "IComponentArguments", "Gets arguments declared and supplied by the parent.", "01"),
        Member("Slots", "IReadOnlyDictionary<string, ComponentSlot>", "Gets the current named slots.", "02"),
        Member("Attributes", "IComponentAttributeCollection", "Gets undeclared fallthrough attributes.", "03"),
        Member("Components", "IComponentFactory", "Gets the application-selected component resolver.", "04"),
        Member("Services", "IServiceProvider", "Gets the application service resolver.", "05"),
        Member("Lifecycle", "IComponentLifecycle", "Gets lifecycle registration and component cancellation.", "06"),
        Method("Emit", "void Emit(string eventName, params object?[] arguments)", "Emits a component event to the parent.", "07"),
        Method("Expose", "void Expose(object? value)", "Selects the public template-reference surface.", "08"),
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
        Method("StartBatch", "void StartBatch()", "Begins batched reactive notification.", "09"),
        Method("EndBatch", "void EndBatch()", "Completes batched reactive notification.", "10"),
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

    private static LanguageCompletionItem Text(
        string label,
        LanguageCompletionItemKind kind,
        string detail,
        string documentation,
        string sortText)
        => new(label, kind, detail, documentation, label, false, sortText);

    private static LanguageCompletionItem Tag(
        string label,
        string documentation,
        string sortText,
        bool selfClosing = false)
        => Snippet(
            label,
            "Template element",
            documentation,
            selfClosing ? $"<{label} $1/>" : $"<{label}$1>$0</{label}>",
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

    private static LanguageCompletionItem CssProperty(
        string label,
        string documentation,
        string sortText)
        => Snippet(label, "CSS property", documentation, $"{label}: $1;", sortText);
}
