using System;
using System.IO;

using Assimalign.Viu.Syntax.SingleFileComponent;

namespace Assimalign.Viu.Tooling.LanguageService;

internal sealed class LanguageDocument
{
    private LanguageDocument(
        string documentUri,
        string text,
        int? version,
        LanguageDocumentSyntax syntax)
    {
        DocumentUri = documentUri;
        Text = text;
        Version = version;
        Syntax = syntax;
    }

    internal string DocumentUri { get; }

    internal string Text { get; }

    internal int? Version { get; }

    internal LanguageDocumentSyntax Syntax { get; }

    internal static LanguageDocument Create(string documentUri, string text, int? version)
    {
        if (HasVueExtension(documentUri))
        {
            var result = VueSingleFileComponentParser.Parse(text);
            return new LanguageDocument(
                documentUri,
                text,
                version,
                new LanguageDocumentSyntax(
                    LanguageDocumentFormat.Vue,
                    result.Descriptor.Template,
                    result.Descriptor.Script,
                    result.Descriptor.ScriptSetup,
                    result.Descriptor.Styles,
                    result.Descriptor.CustomBlocks,
                    result.Errors));
        }

        var viuResult = SingleFileComponentParser.Parse(text);
        return new LanguageDocument(
            documentUri,
            text,
            version,
            new LanguageDocumentSyntax(
                LanguageDocumentFormat.Viu,
                viuResult.Descriptor.Template,
                viuResult.Descriptor.Script,
                null,
                viuResult.Descriptor.Styles,
                viuResult.Descriptor.CustomBlocks,
                viuResult.Errors));
    }

    private static bool HasVueExtension(string documentUri)
    {
        if (Uri.TryCreate(documentUri, UriKind.Absolute, out var uri) &&
            uri.IsFile)
        {
            return string.Equals(
                Path.GetExtension(uri.LocalPath),
                ".vue",
                StringComparison.OrdinalIgnoreCase);
        }

        return documentUri.EndsWith(
            ".vue",
            StringComparison.OrdinalIgnoreCase);
    }
}
