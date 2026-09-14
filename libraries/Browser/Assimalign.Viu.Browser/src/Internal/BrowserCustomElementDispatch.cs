using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace Assimalign.Viu.Browser;

[SupportedOSPlatform("browser")]
internal static partial class BrowserCustomElementDispatch
{
    [JSExport]
    internal static void Dispatch(
        [JSMarshalAs<JSType.Array<JSType.Number>>] int[] operations,
        [JSMarshalAs<JSType.Array<JSType.Number>>] int[] definitionIdentifiers,
        [JSMarshalAs<JSType.Array<JSType.Number>>] int[] elementIdentifiers,
        [JSMarshalAs<JSType.Array<JSType.Number>>] int[] containerHandles,
        [JSMarshalAs<JSType.Array<JSType.String>>] string[] names,
        [JSMarshalAs<JSType.Array<JSType.Any>>] object?[] values)
        => BrowserCustomElements.Active?.Dispatch(operations, definitionIdentifiers,
            elementIdentifiers, containerHandles, names, values);
}
