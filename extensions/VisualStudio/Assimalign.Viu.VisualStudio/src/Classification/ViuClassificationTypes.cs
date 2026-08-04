using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Registers the classification types Viu owns.
/// </summary>
/// <remarks>
/// <para>
/// These are the template, markup, and style categories of the Viu palette. Everything Viu can say
/// about embedded C# is expressed with the classification types the editor and Roslyn already
/// register, so a script span inherits whatever colors the user chose for C#; those names are not
/// re-registered here. <see cref="ViuClassificationTypeNames"/> holds the whole mapping.
/// </para>
/// <para>
/// Each type derives from <c>text</c> so it has sensible properties even before the paired
/// <see cref="ClassificationFormatDefinition"/> is applied, and each has exactly one format
/// definition supplying its default color. The formats are user-visible: the defaults chosen here are
/// a starting point, and Tools &gt; Options &gt; Fonts and Colors is the supported way to change them.
/// </para>
/// </remarks>
internal static class ViuClassificationTypes
{
    [Export]
    [Name(ViuClassificationTypeNames.Tag)]
    [BaseDefinition("text")]
    internal static readonly ClassificationTypeDefinition? Tag = null;

    [Export]
    [Name(ViuClassificationTypeNames.Element)]
    [BaseDefinition("text")]
    internal static readonly ClassificationTypeDefinition? Element = null;

    [Export]
    [Name(ViuClassificationTypeNames.Component)]
    [BaseDefinition("text")]
    internal static readonly ClassificationTypeDefinition? Component = null;

    [Export]
    [Name(ViuClassificationTypeNames.Directive)]
    [BaseDefinition("text")]
    internal static readonly ClassificationTypeDefinition? Directive = null;

    [Export]
    [Name(ViuClassificationTypeNames.Attribute)]
    [BaseDefinition("text")]
    internal static readonly ClassificationTypeDefinition? Attribute = null;

    [Export]
    [Name(ViuClassificationTypeNames.AttributeValue)]
    [BaseDefinition("text")]
    internal static readonly ClassificationTypeDefinition? AttributeValue = null;

    [Export]
    [Name(ViuClassificationTypeNames.InterpolationDelimiter)]
    [BaseDefinition("text")]
    internal static readonly ClassificationTypeDefinition? InterpolationDelimiter = null;

    [Export]
    [Name(ViuClassificationTypeNames.Delimiter)]
    [BaseDefinition("text")]
    internal static readonly ClassificationTypeDefinition? Delimiter = null;

    [Export]
    [Name(ViuClassificationTypeNames.StyleSelector)]
    [BaseDefinition("text")]
    internal static readonly ClassificationTypeDefinition? StyleSelector = null;

    [Export]
    [Name(ViuClassificationTypeNames.StyleCustomProperty)]
    [BaseDefinition("text")]
    internal static readonly ClassificationTypeDefinition? StyleCustomProperty = null;
}
