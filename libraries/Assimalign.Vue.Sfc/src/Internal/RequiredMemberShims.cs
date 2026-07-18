// netstandard2.0 shims enabling the C# `required` member feature used by the immutable block models.
// These attributes ship in the BCL only from net7.0 onward; on the netstandard2.0 analyzer surface the
// compiler still recognises them by full name, so declaring them here lights up `required` without any
// runtime dependency. Referenced only by the C# compiler; never used at run time. Mirrors the shims in
// Assimalign.Vue.Compiler.

namespace System.Runtime.CompilerServices
{
    /// <summary>Marks a type or member as requiring its <c>required</c> members to be initialised.</summary>
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property,
        AllowMultiple = false,
        Inherited = false)]
    internal sealed class RequiredMemberAttribute : Attribute
    {
    }

    /// <summary>Indicates a compiler feature name a consumer must understand to use the annotated element.</summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
    internal sealed class CompilerFeatureRequiredAttribute : Attribute
    {
        /// <summary>Creates the attribute for the named compiler feature.</summary>
        /// <param name="featureName">The required feature name (e.g. <c>RequiredMembers</c>).</param>
        public CompilerFeatureRequiredAttribute(string featureName) => FeatureName = featureName;

        /// <summary>The required feature name.</summary>
        public string FeatureName { get; }

        /// <summary>Whether an unrecognised feature can be safely ignored.</summary>
        public bool IsOptional { get; init; }
    }
}

namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>Signals that a constructor initialises all <c>required</c> members of its type.</summary>
    [AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
    internal sealed class SetsRequiredMembersAttribute : Attribute
    {
    }
}
