using System;

namespace Assimalign.Viu.Browser;

/// <summary>Resolves Browser's compiler-known directive token types without reflection.</summary>
internal sealed class BrowserDirectiveResolver : IDirectiveResolver
{
    public static BrowserDirectiveResolver Instance { get; } = new();

    private BrowserDirectiveResolver()
    {
    }

    public IDirective? Resolve(Type directiveType)
    {
        ArgumentNullException.ThrowIfNull(directiveType);
        if (directiveType == typeof(VModelText))
        {
            return VModelText.Instance;
        }

        if (directiveType == typeof(VModelCheckbox))
        {
            return VModelCheckbox.Instance;
        }

        if (directiveType == typeof(VModelRadio))
        {
            return VModelRadio.Instance;
        }

        if (directiveType == typeof(VModelSelect))
        {
            return VModelSelect.Instance;
        }

        if (directiveType == typeof(VModelDynamic))
        {
            return VModelDynamic.Instance;
        }

        if (directiveType == typeof(CssVariables))
        {
            return CssVariables.Instance;
        }

        return directiveType == typeof(VShow) ? VShow.Instance : null;
    }
}
