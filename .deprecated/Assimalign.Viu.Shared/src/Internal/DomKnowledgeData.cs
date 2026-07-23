// LINKED SHARED-SOURCE SEAM — [V01.01.01.03] decision: this file is the single authoritative
// definition of the DOM knowledge lists, written against the lowest common denominator
// (netstandard2.0-compatible C#: constants only, no modern BCL APIs) so the Roslyn template
// compiler (a netstandard2.0 generator host) links THIS FILE via <Compile Include="..."/>
// while the net10.0 runtime exposes it through DomKnowledge's frozen lookups. Multi-targeting
// the whole Shared assembly was rejected: it would tax every hot-path file with polyfills for
// one consumer. See the Assimalign.Viu.Shared.GeneratorFixture test project, which proves the
// generator-context consumption path compiles and agrees with the runtime tables.

namespace Assimalign.Viu.Shared;

/// <summary>
/// The raw comma-joined DOM knowledge lists mirroring <c>@vue/shared</c>'s
/// <c>domTagConfig.ts</c>/<c>domAttrConfig.ts</c> (upstream's <c>makeMap</c> input format,
/// cross-checked against WHATWG HTML for void elements and boolean attributes).
/// </summary>
internal static class DomKnowledgeData
{
    /// <summary>Upstream <c>HTML_TAGS</c>.</summary>
    internal const string HtmlTags =
        "html,body,base,head,link,meta,style,title,address,article,aside,footer,header,hgroup,"
        + "h1,h2,h3,h4,h5,h6,nav,section,div,dd,dl,dt,figcaption,figure,picture,hr,img,li,main,"
        + "ol,p,pre,ul,a,b,abbr,bdi,bdo,br,cite,code,data,dfn,em,i,kbd,mark,q,rp,rt,ruby,s,samp,"
        + "small,span,strong,sub,sup,time,u,var,wbr,area,audio,map,track,video,embed,object,"
        + "param,source,canvas,script,noscript,del,ins,caption,col,colgroup,table,thead,tbody,td,"
        + "th,tr,button,datalist,fieldset,form,input,label,legend,meter,optgroup,option,output,"
        + "progress,select,textarea,details,dialog,menu,summary,template,blockquote,iframe,tfoot";

    /// <summary>Upstream <c>SVG_TAGS</c> (case-sensitive per the SVG 2 element tables).</summary>
    internal const string SvgTags =
        "svg,animate,animateMotion,animateTransform,circle,clipPath,color-profile,defs,desc,"
        + "discard,ellipse,feBlend,feColorMatrix,feComponentTransfer,feComposite,feConvolveMatrix,"
        + "feDiffuseLighting,feDisplacementMap,feDistantLight,feDropShadow,feFlood,feFuncA,"
        + "feFuncB,feFuncG,feFuncR,feGaussianBlur,feImage,feMerge,feMergeNode,feMorphology,"
        + "feOffset,fePointLight,feSpecularLighting,feSpotLight,feTile,feTurbulence,filter,"
        + "foreignObject,g,hatch,hatchpath,image,line,linearGradient,marker,mask,mesh,"
        + "meshgradient,meshpatch,meshrow,metadata,mpath,path,pattern,polygon,polyline,"
        + "radialGradient,rect,set,solidcolor,stop,switch,symbol,text,textPath,title,tspan,"
        + "unknown,use,view";

    /// <summary>Upstream <c>MATH_TAGS</c> (MathML Core element tables).</summary>
    internal const string MathTags =
        "annotation,annotation-xml,maction,maligngroup,malignmark,math,menclose,merror,mfenced,"
        + "mfrac,mfraction,mglyph,mi,mlabeledtr,mlongdiv,mmultiscripts,mn,mo,mover,mpadded,"
        + "mphantom,mprescripts,mroot,mrow,ms,mscarries,mscarry,msgroup,msline,mspace,msqrt,"
        + "msrow,mstack,mstyle,msub,msubsup,msup,mtable,mtd,mtext,mtr,munder,munderover,none,"
        + "semantics";

    /// <summary>Upstream <c>VOID_TAGS</c> (WHATWG void elements).</summary>
    internal const string VoidTags = "area,base,br,col,embed,hr,img,input,link,meta,param,source,track,wbr";

    /// <summary>
    /// Upstream <c>isBooleanAttr</c>'s input: the special boolean attributes plus the common
    /// set (WHATWG boolean attributes).
    /// </summary>
    internal const string BooleanAttributes =
        "itemscope,allowfullscreen,formnovalidate,ismap,nomodule,novalidate,readonly,"
        + "async,autofocus,autoplay,controls,default,defer,disabled,hidden,inert,loop,open,"
        + "required,reversed,scoped,seamless,checked,muted,multiple,selected";

    /// <summary>Upstream <c>isKnownHtmlAttr</c>'s input.</summary>
    internal const string KnownHtmlAttributes =
        "accept,accept-charset,accesskey,action,align,allow,alt,async,autocapitalize,"
        + "autocomplete,autofocus,autoplay,background,bgcolor,border,buffered,capture,challenge,"
        + "charset,checked,cite,class,code,codebase,color,cols,colspan,content,contenteditable,"
        + "contextmenu,controls,coords,crossorigin,csp,data,datetime,decoding,default,defer,dir,"
        + "dirname,disabled,download,draggable,dropzone,enctype,enterkeyhint,for,form,formaction,"
        + "formenctype,formmethod,formnovalidate,formtarget,headers,height,hidden,high,href,"
        + "hreflang,http-equiv,icon,id,importance,inert,integrity,ismap,itemprop,keytype,kind,"
        + "label,lang,language,loading,list,loop,low,manifest,max,maxlength,minlength,media,min,"
        + "multiple,muted,name,novalidate,open,optimum,pattern,ping,placeholder,poster,preload,"
        + "radiogroup,readonly,referrerpolicy,rel,required,reversed,rows,rowspan,sandbox,scope,"
        + "scoped,selected,shape,size,sizes,slot,span,spellcheck,src,srcdoc,srclang,srcset,start,"
        + "step,style,summary,tabindex,target,title,translate,type,usemap,value,width,wrap";

    /// <summary>Upstream <c>isKnownSvgAttr</c>'s input (SVG 2 attribute tables).</summary>
    internal const string KnownSvgAttributes =
        "xmlns,accent-height,accumulate,additive,alignment-baseline,alphabetic,amplitude,"
        + "arabic-form,ascent,attributeName,attributeType,azimuth,baseFrequency,baseline-shift,"
        + "baseProfile,bbox,begin,bias,by,calcMode,cap-height,class,clip,clipPathUnits,clip-path,"
        + "clip-rule,color,color-interpolation,color-interpolation-filters,color-profile,"
        + "color-rendering,contentScriptType,contentStyleType,crossorigin,cursor,cx,cy,d,"
        + "decelerate,descent,diffuseConstant,direction,display,divisor,dominant-baseline,dur,dx,"
        + "dy,edgeMode,elevation,enable-background,end,exponent,fill,fill-opacity,fill-rule,"
        + "filter,filterRes,filterUnits,flood-color,flood-opacity,font-family,font-size,"
        + "font-size-adjust,font-stretch,font-style,font-variant,font-weight,format,from,fr,fx,"
        + "fy,g1,g2,glyph-name,glyph-orientation-horizontal,glyph-orientation-vertical,"
        + "glyphRef,gradientTransform,gradientUnits,hanging,height,href,hreflang,horiz-adv-x,"
        + "horiz-origin-x,id,ideographic,image-rendering,in,in2,intercept,k,k1,k2,k3,k4,"
        + "kernelMatrix,kernelUnitLength,kerning,keyPoints,keySplines,keyTimes,lang,"
        + "lengthAdjust,letter-spacing,lighting-color,limitingConeAngle,local,marker-end,"
        + "marker-mid,marker-start,markerHeight,markerUnits,markerWidth,mask,maskContentUnits,"
        + "maskUnits,mathematical,max,media,method,min,mode,name,numOctaves,offset,opacity,"
        + "operator,order,orient,orientation,origin,overflow,overline-position,"
        + "overline-thickness,panose-1,paint-order,path,pathLength,patternContentUnits,"
        + "patternTransform,patternUnits,ping,pointer-events,points,pointsAtX,pointsAtY,"
        + "pointsAtZ,preserveAlpha,preserveAspectRatio,primitiveUnits,r,radius,referrerPolicy,"
        + "refX,refY,rel,rendering-intent,repeatCount,repeatDur,requiredExtensions,"
        + "requiredFeatures,restart,result,rotate,rx,ry,scale,seed,shape-rendering,slope,"
        + "spacing,specularConstant,specularExponent,speed,spreadMethod,startOffset,"
        + "stdDeviation,stemh,stemv,stitchTiles,stop-color,stop-opacity,"
        + "strikethrough-position,strikethrough-thickness,string,stroke,stroke-dasharray,"
        + "stroke-dashoffset,stroke-linecap,stroke-linejoin,stroke-miterlimit,stroke-opacity,"
        + "stroke-width,style,surfaceScale,systemLanguage,tabindex,tableValues,target,targetX,"
        + "targetY,text-anchor,text-decoration,text-rendering,textLength,to,transform,"
        + "transform-origin,type,u1,u2,underline-position,underline-thickness,unicode,"
        + "unicode-bidi,unicode-range,units-per-em,v-alphabetic,v-hanging,v-ideographic,"
        + "v-mathematical,values,vector-effect,version,vert-adv-y,vert-origin-x,vert-origin-y,"
        + "viewBox,viewTarget,visibility,width,widths,word-spacing,writing-mode,x,x-height,x1,"
        + "x2,xChannelSelector,xlink:actuate,xlink:arcrole,xlink:href,xlink:role,xlink:show,"
        + "xlink:title,xlink:type,xmlns:xlink,xml:base,xml:lang,xml:space,y,y1,y2,"
        + "yChannelSelector,z,zoomAndPan";

    /// <summary>
    /// Upstream's unsafe attribute-name characters (SSR): <c>&gt;</c>, <c>/</c>, <c>=</c>,
    /// quotes, tab, newline, form feed, and space.
    /// </summary>
    internal const string UnsafeAttributeNameCharacters = ">/=\"'\u0009\u000A\u000C\u0020";

    /// <summary>
    /// Upstream <c>propsToAttrMap</c>: the camelCase prop → attribute casing exceptions, as
    /// alternating name/value entries.
    /// </summary>
    internal static readonly string[] PropertyToAttributePairs =
    [
        "acceptCharset", "accept-charset",
        "className", "class",
        "htmlFor", "for",
        "httpEquiv", "http-equiv",
    ];
}
