namespace Assimalign.Vue.SingleFileComponent;

/// <summary>
/// A <c>@script</c> block — the component's C# body (its partial-class body). Mirrors Vue 3.5's
/// <c>SFCScriptBlock</c> (<c>@vue/compiler-sfc</c>); the C# is <em>not</em> analysed here. The Vue
/// <c>&lt;script setup&gt;</c> counterpart (setup-vs-options distinction) is script analysis and is
/// deferred to [V01.01.06.03]. See https://vuejs.org/api/sfc-spec.html#script.
/// </summary>
public sealed record SingleFileComponentScriptBlock : SingleFileComponentBlock
{
    /// <inheritdoc />
    public override SingleFileComponentBlockKind Kind => SingleFileComponentBlockKind.Script;
}
