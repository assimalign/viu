namespace Assimalign.Viu.LanguageService;

/// <summary>
/// Classification-type names emitted for projected C# identifiers.
/// </summary>
/// <remarks>
/// These are the classification names used by the C# editor. Keeping them in the host-neutral
/// language-service contract lets a projected <c>@script</c> span carry the same semantic category
/// as the identical token in a plain C# document. [V01.01.12.07.11]
/// </remarks>
public static class LanguageClassificationTypeNames
{
    /// <summary>A namespace name.</summary>
    public const string NamespaceName = "namespace name";

    /// <summary>A class name, including an attribute class name.</summary>
    public const string ClassName = "class name";

    /// <summary>A delegate type name.</summary>
    public const string DelegateName = "delegate name";

    /// <summary>An enum type name.</summary>
    public const string EnumName = "enum name";

    /// <summary>An interface type name.</summary>
    public const string InterfaceName = "interface name";

    /// <summary>A struct type name.</summary>
    public const string StructName = "struct name";

    /// <summary>A type-parameter name.</summary>
    public const string TypeParameterName = "type parameter name";

    /// <summary>A method or local-function name.</summary>
    public const string MethodName = "method name";

    /// <summary>A property name in either a declaration or a reference.</summary>
    public const string PropertyName = "property name";

    /// <summary>A field name.</summary>
    public const string FieldName = "field name";

    /// <summary>A constant name.</summary>
    public const string ConstantName = "constant name";

    /// <summary>An enum-member name.</summary>
    public const string EnumMemberName = "enum member name";

    /// <summary>An event name.</summary>
    public const string EventName = "event name";

    /// <summary>A parameter name in either a declaration or a reference.</summary>
    public const string ParameterName = "parameter name";

    /// <summary>A local or range-variable name in either a declaration or a reference.</summary>
    public const string LocalName = "local name";

    /// <summary>A label name.</summary>
    public const string LabelName = "label name";
}
