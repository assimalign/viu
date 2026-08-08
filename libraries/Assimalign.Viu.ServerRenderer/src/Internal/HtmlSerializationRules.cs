using System;
using System.Collections.Frozen;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>Owns the WHATWG element and attribute policy needed only by HTML serialization.</summary>
internal static class HtmlSerializationRules
{
    private const string HtmlNamespace = "http://www.w3.org/1999/xhtml";
    private const string VoidElements =
        "area,base,br,col,embed,hr,img,input,link,meta,param,source,track,wbr";
    private const string BooleanAttributes =
        "itemscope,allowfullscreen,formnovalidate,ismap,nomodule,novalidate,readonly,"
        + "async,autofocus,autoplay,controls,default,defer,disabled,hidden,inert,loop,open,"
        + "required,reversed,scoped,seamless,checked,muted,multiple,selected";
    private const string SvgElements =
        "svg,animate,animateMotion,animateTransform,circle,clipPath,color-profile,defs,desc,"
        + "discard,ellipse,feBlend,feColorMatrix,feComponentTransfer,feComposite,feConvolveMatrix,"
        + "feDiffuseLighting,feDisplacementMap,feDistantLight,feDropShadow,feFlood,feFuncA,"
        + "feFuncB,feFuncG,feFuncR,feGaussianBlur,feImage,feMerge,feMergeNode,feMorphology,"
        + "feOffset,fePointLight,feSpecularLighting,feSpotLight,feTile,feTurbulence,filter,"
        + "foreignObject,g,hatch,hatchpath,image,line,linearGradient,marker,mask,mesh,"
        + "meshgradient,meshpatch,meshrow,metadata,mpath,path,pattern,polygon,polyline,"
        + "radialGradient,rect,set,solidcolor,stop,switch,symbol,text,textPath,title,tspan,"
        + "unknown,use,view";

    private static readonly FrozenSet<string> VoidElementSet =
        VoidElements.Split(',').ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    private static readonly FrozenSet<string> BooleanAttributeSet =
        BooleanAttributes.Split(',').ToFrozenSet(StringComparer.Ordinal);
    private static readonly FrozenSet<string> SvgElementSet =
        SvgElements.Split(',').ToFrozenSet(StringComparer.Ordinal);

    internal static bool IsVoidElement(QualifiedName name) =>
        IsHtmlNamespace(name.NamespaceName) && VoidElementSet.Contains(name.LocalName);

    internal static bool IsBooleanAttribute(string name) => BooleanAttributeSet.Contains(name);

    internal static bool ShouldPreserveAttributeCase(QualifiedName elementName) =>
        !IsHtmlNamespace(elementName.NamespaceName)
        || elementName.LocalName.IndexOf('-', StringComparison.Ordinal) > 0
        || IsSvgElement(elementName.LocalName);

    internal static bool ShouldPreserveAttributeCase(string? elementName) =>
        elementName is not null
        && (elementName.IndexOf('-', StringComparison.Ordinal) > 0
            || IsSvgElement(elementName));

    internal static bool IsSsrSafeAttributeName(string name)
    {
        if (name.Length == 0)
        {
            return false;
        }

        for (int index = 0; index < name.Length; index++)
        {
            char character = name[index];
            if (char.IsControl(character)
                || char.IsWhiteSpace(character)
                || character is '>' or '/' or '=' or '"' or '\'')
            {
                return false;
            }
        }

        return true;
    }

    internal static string GetAttributeName(string propertyName) => propertyName switch
    {
        "acceptCharset" => "accept-charset",
        "className" => "class",
        "htmlFor" => "for",
        "httpEquiv" => "http-equiv",
        _ => propertyName,
    };

    private static bool IsHtmlNamespace(string? namespaceName) =>
        namespaceName is null
        || string.Equals(namespaceName, HtmlNamespace, StringComparison.Ordinal);

    private static bool IsSvgElement(string name) => SvgElementSet.Contains(name);
}
