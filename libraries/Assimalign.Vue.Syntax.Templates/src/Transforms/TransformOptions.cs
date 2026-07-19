using System;
using System.Collections.Generic;

namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// Configures <see cref="Transformer"/>. The C# port of the transform-relevant members of Vue 3.5's
/// <c>TransformOptions</c> (<c>@vue/compiler-core</c> <c>options.ts</c>). Defaults reproduce the platform-
/// agnostic base compiler; <see cref="CreateDom"/> installs the DOM directive transforms mirroring
/// <c>@vue/compiler-dom</c>.
/// </summary>
public sealed class TransformOptions
{
    /// <summary>
    /// Additional node transforms, appended after the built-in preset (upstream's user
    /// <c>nodeTransforms</c>). Lets third parties inject custom transforms.
    /// </summary>
    public IReadOnlyList<NodeTransform> NodeTransforms { get; set; } = Array.Empty<NodeTransform>();

    /// <summary>
    /// Additional or overriding directive transforms, keyed by directive name (upstream's user
    /// <c>directiveTransforms</c>). Entries override the built-in transforms of the same name.
    /// </summary>
    public IReadOnlyDictionary<string, DirectiveTransform> DirectiveTransforms { get; set; }
        = new Dictionary<string, DirectiveTransform>();

    /// <summary>
    /// Resolves a built-in component tag to its runtime helper, or <see langword="null"/> (upstream
    /// <c>isBuiltInComponent</c>).
    /// </summary>
    public Func<string, RuntimeHelper?>? IsBuiltInComponent { get; set; }

    /// <summary>Whether a tag is a custom element (never a component); defaults to never (upstream <c>isCustomElement</c>).</summary>
    public Func<string, bool> IsCustomElement { get; set; } = static _ => false;

    /// <summary>Receives transform diagnostics. Defaults to swallowing them (recoverable).</summary>
    public Action<CompilerError>? OnError { get; set; }

    /// <summary>
    /// Whether the expression and scope analysis pass runs (upstream <c>prefixIdentifiers</c>). Defaults to
    /// <see langword="false"/>, keeping expression bodies opaque like Vue's browser build; set it, together with
    /// <see cref="BindingMetadata"/>, to enable identifier classification and <c>Ref&lt;T&gt;</c> unwrapping
    /// ([V01.01.05.04]).
    /// </summary>
    public bool PrefixIdentifiers { get; set; }

    /// <summary>
    /// The component binding classifications expression rewriting resolves identifiers against (upstream
    /// <c>bindingMetadata</c>), produced by the component/setup source model. Defaults to
    /// <see cref="BindingMetadata.Empty"/> and is only consulted when <see cref="PrefixIdentifiers"/> is set.
    /// </summary>
    public BindingMetadata? BindingMetadata { get; set; }

    /// <summary>
    /// The CSS Modules accessors ([V01.01.05.04.01]) expression classification resolves <c>$style</c> (and
    /// named-module) references against — the Vuecs stand-in for Vue's runtime <c>$style</c> object, supplied by
    /// the composition-root generator ([V01.01.06.06]). Defaults to <see cref="CssModuleAccessors.Empty"/> and is
    /// only consulted when <see cref="PrefixIdentifiers"/> is set.
    /// </summary>
    public CssModuleAccessors? CssModules { get; set; }

    /// <summary>
    /// How a rewritten binding spells its receiver (upstream has no counterpart — one render context). Defaults to
    /// <see cref="BindingRewriteMode.RenderContext"/> (the <c>_ctx.</c> render form); the composition-root
    /// generator sets <see cref="BindingRewriteMode.InstanceMember"/> only for the standalone <c>v-bind()</c> CSS
    /// getter compile ([V01.01.06.06.01]).
    /// </summary>
    public BindingRewriteMode BindingRewriteMode { get; set; } = BindingRewriteMode.RenderContext;

    /// <summary>
    /// Whether static event handlers are cached so blocks are not invalidated (upstream <c>cacheHandlers</c>).
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool CacheHandlers { get; set; }

    /// <summary>
    /// Whether the static-hoisting/caching pass runs. Defaults to <see langword="false"/>; the pass itself is
    /// [V01.01.05.07], which consumes the hooks this transform exposes.
    /// </summary>
    public bool HoistStatic { get; set; }

    /// <summary>Whether compilation targets SSR. Defaults to <see langword="false"/>.</summary>
    public bool Ssr { get; set; }

    /// <summary>Whether this is a nested SSR slot compilation. Defaults to <see langword="false"/>.</summary>
    public bool InSSR { get; set; }

    /// <summary>Whether component slots inherit the parent's scope id. Defaults to <see langword="true"/>.</summary>
    public bool Slotted { get; set; } = true;

    /// <summary>The scoped-styles id, or <see langword="null"/>. Out of scope here; carried for parity.</summary>
    public string? ScopeId { get; set; }

    /// <summary>
    /// Creates transform options with the DOM component defaults: <c>Transition</c>/<c>TransitionGroup</c>
    /// recognized as built-in components, and <paramref name="isCustomElement"/> reporting custom elements.
    /// The DOM directive transforms (<c>v-model</c>, <c>v-on</c>, <c>v-show</c>, <c>v-html</c>, <c>v-text</c>,
    /// <c>v-cloak</c>) are always installed as the pipeline's built-ins, so this only configures component
    /// recognition. Mirrors the setup <c>@vue/compiler-dom</c> applies in its <c>compile()</c>.
    /// </summary>
    /// <param name="isCustomElement">Whether a tag is a custom element; defaults to never.</param>
    public static TransformOptions CreateDom(Func<string, bool>? isCustomElement = null) => new()
    {
        IsCustomElement = isCustomElement ?? (static _ => false),
        IsBuiltInComponent = static tag => tag switch
        {
            "Transition" or "transition" => HelperNames.Transition,
            "TransitionGroup" or "transition-group" => HelperNames.TransitionGroup,
            _ => null,
        },
    };
}
