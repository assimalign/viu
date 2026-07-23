using System;

using Assimalign.Viu;

namespace Assimalign.Viu.Browser;

/// <summary>
/// Resolves the browser directives emitted by the template compiler.
/// </summary>
/// <remarks>
/// The browser builder installs this resolver by default. Applications remain free to replace it
/// through <see cref="ApplicationBuilder.UseDirectiveResolver(IDirectiveResolver)"/>.
/// </remarks>
internal sealed class BrowserDirectiveResolver : IDirectiveResolver
{
    internal static readonly BrowserDirectiveResolver Instance = new();

    private BrowserDirectiveResolver()
    {
    }

    /// <inheritdoc/>
    public IDirective? Resolve(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return name switch
        {
            "show" => VShow.Instance,
            "modelText" => VModelText.Instance,
            "modelCheckbox" => VModelCheckbox.Instance,
            "modelRadio" => VModelRadio.Instance,
            "modelSelect" => VModelSelect.Instance,
            "modelDynamic" => VModelDynamic.Instance,
            _ => null,
        };
    }
}
