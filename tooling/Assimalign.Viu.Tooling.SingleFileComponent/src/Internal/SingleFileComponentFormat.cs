namespace Assimalign.Viu.Tooling.SingleFileComponent;

/// <summary>The outer container format of an additional single-file-component source.</summary>
internal enum SingleFileComponentFormat
{
    /// <summary>Viu's canonical <c>.viu</c> <c>@</c>-block container.</summary>
    Viu,

    /// <summary>The [V01.01.06.09] tag-based <c>.vue</c> compatibility container.</summary>
    Vue,
}
