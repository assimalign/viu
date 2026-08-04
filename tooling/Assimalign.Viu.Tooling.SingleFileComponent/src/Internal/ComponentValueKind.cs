namespace Assimalign.Viu.Tooling.SingleFileComponent;

/// <summary>
/// How much a declared parameter's C# type — or a template-supplied value's type — is known to the
/// build-time checker. The projection classifies types <em>syntactically</em> (it has no semantic model
/// for the code it is itself generating), so anything it cannot decide from the spelling alone stays
/// <see cref="Unknown"/> and is never reported.
/// </summary>
internal enum ComponentValueKind
{
    /// <summary>The type is not decidable from the declaration alone; no compatibility check runs.</summary>
    Unknown = 0,

    /// <summary>The type is <c>string</c> (nullable or not).</summary>
    Text = 1,

    /// <summary>
    /// The type is a C# predefined value type (or the nullable form of one): no string, and no
    /// reference value, can ever be assigned to it.
    /// </summary>
    Value = 2,

    /// <summary>The type is a reference type that is not <c>string</c>.</summary>
    Reference = 3,

    /// <summary>The type is <c>object</c> (or <c>dynamic</c>): every value is assignable.</summary>
    Any = 4,
}
