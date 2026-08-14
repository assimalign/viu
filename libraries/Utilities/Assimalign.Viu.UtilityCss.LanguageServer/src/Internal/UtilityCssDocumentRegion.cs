using System;
using System.IO;

using Assimalign.Viu.Syntax.SingleFileComponent;

namespace Assimalign.Viu.UtilityCss.LanguageServer;

internal readonly record struct UtilityCssDocumentRegion(
    string Content,
    int ContentOffset)
{
    internal static bool IsSupported(string documentUri)
        => TryGetExtension(documentUri, out var extension) &&
           extension is ".viu" or ".vue" or ".razor" or ".cshtml" or ".html" or ".htm";

    internal static bool TryCreate(
        string documentUri,
        string documentText,
        out UtilityCssDocumentRegion region)
    {
        region = default;
        if (!TryGetExtension(documentUri, out var extension))
        {
            return false;
        }

        if (extension == ".viu")
        {
            var template = SingleFileComponentParser.Parse(documentText).Descriptor.Template;
            if (template is null)
            {
                return false;
            }

            region = new UtilityCssDocumentRegion(
                template.Content,
                template.ContentLocation.Start.Offset);
            return true;
        }

        if (extension == ".vue")
        {
            var template = VueSingleFileComponentParser.Parse(documentText).Descriptor.Template;
            if (template is null)
            {
                return false;
            }

            region = new UtilityCssDocumentRegion(
                template.Content,
                template.ContentLocation.Start.Offset);
            return true;
        }

        if (extension is not (".razor" or ".cshtml" or ".html" or ".htm"))
        {
            return false;
        }

        region = new UtilityCssDocumentRegion(documentText, 0);
        return true;
    }

    internal bool TryGetContentPosition(
        int documentPosition,
        out int contentPosition)
    {
        contentPosition = documentPosition - ContentOffset;
        return contentPosition >= 0 && contentPosition <= Content.Length;
    }

    private static bool TryGetExtension(
        string documentUri,
        out string extension)
    {
        string path;
        if (Uri.TryCreate(documentUri, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            path = uri.LocalPath;
        }
        else
        {
            path = documentUri;
        }

        extension = Path.GetExtension(path).ToLowerInvariant();
        return extension.Length > 0;
    }
}
