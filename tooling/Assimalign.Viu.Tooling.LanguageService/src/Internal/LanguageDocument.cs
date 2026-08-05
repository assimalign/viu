using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using Assimalign.Viu.Syntax.SingleFileComponent;

namespace Assimalign.Viu.Tooling.LanguageService;

internal sealed class LanguageDocument
{
    // Template element folding ([V01.01.12.07.07]) needs the block's markup parsed, which the
    // container parse deliberately does not do. The document snapshot is immutable and replaced
    // whole on every edit, so computing the ranges once here keeps the per-request contract: an
    // unedited document answers every folding request from one template parse. Publication is
    // thread-safe because reads run outside the service's synchronization lock.
    private readonly Lazy<IReadOnlyList<LanguageFoldingRange>> templateElementFoldingRanges;

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
        templateElementFoldingRanges = new Lazy<IReadOnlyList<LanguageFoldingRange>>(
            () => TemplateFoldingRangeCollector.Collect(syntax.Template),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    internal string DocumentUri { get; }

    internal string Text { get; }

    internal int? Version { get; }

    internal LanguageDocumentSyntax Syntax { get; }

    /// <summary>
    /// The folding ranges for the multi-line elements inside this document's template block, in
    /// document order and in the document's own line coordinates. Computed on first use and cached
    /// for the life of the snapshot.
    /// </summary>
    internal IReadOnlyList<LanguageFoldingRange> TemplateElementFoldingRanges
        => templateElementFoldingRanges.Value;

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
