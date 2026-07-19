namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// How expression classification spells the receiver of a rewritten component binding. Vue's single render
/// context has no such choice — every binding resolves through the runtime proxy — but Vuecs generates code into
/// two distinct member contexts, so the compiler must decide the receiver form for each.
/// </summary>
public enum BindingRewriteMode
{
    /// <summary>
    /// The render function ([V01.01.05.05]): a static method receiving the component as <c>_ctx</c>, so every
    /// binding reads through <c>_ctx.</c> and a maybe/let binding guards its read through <c>unref</c>. The
    /// default and the mode the whole template pipeline uses.
    /// </summary>
    RenderContext,

    /// <summary>
    /// An instance member of the generated component partial class ([V01.01.06.06.01]) — the <c>v-bind()</c> CSS
    /// getter — where bindings read through the implicit <c>this</c> (no <c>_ctx</c>). Only a definite
    /// <see cref="BindingType.SetupReference"/> unwraps (<c>name.Value</c>); every other classification reads bare,
    /// because the generator marks a binding <c>SetupReference</c> exactly when its declared type is a reactive
    /// reference — every other binding it produces is provably non-reactive, so <c>unref</c> would be a no-op and
    /// the getter needs no runtime-helper import.
    /// </summary>
    InstanceMember,
}
