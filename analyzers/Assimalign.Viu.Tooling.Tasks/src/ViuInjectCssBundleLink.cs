using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Assimalign.Viu.Tooling.Tasks;

/// <summary>
/// The MSBuild task that writes a copy of a WebAssembly app's host page (<c>wwwroot/index.html</c>) with a
/// <c>&lt;link rel="stylesheet"&gt;</c> to the bundled <c>.viu</c> <c>@style</c> stylesheet spliced into its
/// <c>&lt;head&gt;</c> — so a consuming app needs no hand-authored link tag ([V01.01.12.12.01], #167). The
/// paired targets in <c>build/Targets/Build.Css.Bundling.targets</c> re-register the written copy as a
/// <c>StaticWebAsset</c> (recomputing its integrity and endpoints) <b>before</b> the SDK's compression
/// pipeline runs, so gzip/brotli variants are derived from the injected content and negotiation is never
/// desynced — the failure mode that made an earlier in-place transformation drop compressed variants.
/// <para>
/// This task only <em>reads</em> the current host page and <em>writes</em> a transformed copy to an
/// intermediate path; it never edits the source file in place. Splicing is idempotent: if the bundle is
/// already referenced (a hand-authored link, or a previous run), the copy is written unchanged and
/// <see cref="LinkInjected"/> is <see langword="false"/>, so an app that opts to keep its own link is never
/// double-injected. The document's newline style and the closing tag's indentation are preserved. This is a
/// build-tooling text transform — it does not parse HTML — so it deliberately matches only the literal
/// <c>&lt;/head&gt;</c> close tag.
/// </para>
/// </summary>
/// <remarks>Single-threaded, invoked once per host page per build; not designed for concurrent use.</remarks>
public sealed class ViuInjectCssBundleLink : Microsoft.Build.Utilities.Task, ICancelableTask
{
    private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();

    /// <summary>
    /// The absolute path of the host page to read (the current registered <c>.html</c> static web asset, e.g.
    /// the SDK's placeholder-resolved <c>index.html</c>). The file is never modified in place.
    /// </summary>
    [Required]
    public string HostPagePath { get; set; } = string.Empty;

    /// <summary>
    /// The absolute path to write the transformed copy to, under the intermediate output directory
    /// (e.g. <c>obj/&lt;config&gt;/&lt;tfm&gt;/viu/htmllink/&lt;kind&gt;/index.html</c>). Its directory is
    /// created if absent. The copy is always written (even when nothing was injected) so the paired target
    /// can re-register a single, stable content root.
    /// </summary>
    [Required]
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// The <c>href</c> to inject — the bundle's resolved static-web-asset route (the stable plain route
    /// <c>&lt;PackageId&gt;.viu.css</c> a statically hosted WASM app serves; see the design doc for why the
    /// fingerprinted route is not injected for standalone hosting).
    /// </summary>
    [Required]
    public string LinkHref { get; set; } = string.Empty;

    /// <summary>
    /// The bundle's plain file name (e.g. <c>&lt;PackageId&gt;.viu.css</c>), used only for the idempotency
    /// check: if the host page already references it, no link is injected. Optional; when empty only an exact
    /// <see cref="LinkHref"/> match suppresses injection.
    /// </summary>
    public string BundleFileName { get; set; } = string.Empty;

    /// <summary>The path actually written — always equal to <see cref="OutputPath"/> on success.</summary>
    [Output]
    public string InjectedHostPagePath { get; set; } = string.Empty;

    /// <summary>
    /// <see langword="true"/> when a link was spliced in this run; <see langword="false"/> when the bundle was
    /// already referenced (so the copy is a verbatim passthrough). Lets the build report real injection.
    /// </summary>
    [Output]
    public bool LinkInjected { get; set; }

    /// <inheritdoc />
    public void Cancel() => _cancellation.Cancel();

    /// <inheritdoc />
    public override bool Execute()
    {
        string html;
        try
        {
            html = File.ReadAllText(HostPagePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.LogErrorFromException(exception, showStackTrace: false);
            return false;
        }

        _cancellation.Token.ThrowIfCancellationRequested();

        var transformed = InjectStylesheetLink(html, LinkHref, BundleFileName, out var injected);

        try
        {
            var directory = Path.GetDirectoryName(OutputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // UTF-8 without a BOM, newlines preserved from the source — File.WriteAllText writes the string
            // bytes verbatim, so the host page's line endings are unchanged.
            File.WriteAllText(OutputPath, transformed, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.LogErrorFromException(exception, showStackTrace: false);
            return false;
        }

        InjectedHostPagePath = OutputPath;
        LinkInjected = injected;
        Log.LogMessage(
            injected ? MessageImportance.Normal : MessageImportance.Low,
            injected
                ? "ViuInjectCssBundleLink: injected <link href=\"{0}\"> into {1}."
                : "ViuInjectCssBundleLink: bundle already referenced; {1} written unchanged.",
            LinkHref,
            OutputPath);
        return true;
    }

    /// <summary>
    /// Splices a <c>&lt;link rel="stylesheet" href="{href}"&gt;</c> immediately before the first
    /// <c>&lt;/head&gt;</c> (case-insensitive), preserving the document's newline style and the close tag's
    /// indentation. Returns the input unchanged (with <paramref name="injected"/> <see langword="false"/>) when
    /// the bundle is already referenced or no <c>&lt;/head&gt;</c> is present.
    /// </summary>
    internal static string InjectStylesheetLink(string html, string href, string bundleFileName, out bool injected)
    {
        injected = false;
        if (string.IsNullOrEmpty(html))
        {
            return html;
        }

        // Idempotent: only an existing <link> whose href references the bundle (hand-authored, or a prior
        // injection) suppresses a second link. A bare mention in prose or a comment must NOT — a host page's
        // own explanatory comment may name the file — so the reference is matched only inside an href="…"
        // attribute value.
        if (ReferencesInHref(html, bundleFileName) || ReferencesInHref(html, href))
        {
            return html;
        }

        var closeHead = Regex.Match(html, "</head\\s*>", RegexOptions.IgnoreCase);
        if (!closeHead.Success)
        {
            // No head to inject into — leave the document untouched rather than guess a location.
            return html;
        }

        var newline = html.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n";
        var indent = LeadingWhitespaceOfLine(html, closeHead.Index);
        var link = "<link rel=\"stylesheet\" href=\"" + href + "\" />";

        // Place the link on its own line at the close tag's indentation, then restore the close tag on the
        // following line so </head> keeps its original position.
        var insertion = link + newline + indent;
        injected = true;
        return html.Substring(0, closeHead.Index) + insertion + html.Substring(closeHead.Index);
    }

    /// <summary>
    /// Whether <paramref name="name"/> appears inside an <c>href="…"</c> attribute value (case-insensitive) —
    /// i.e. the bundle is actually linked, not merely mentioned in text.
    /// </summary>
    private static bool ReferencesInHref(string html, string name)
    {
        return !string.IsNullOrEmpty(name)
            && Regex.IsMatch(html, "href\\s*=\\s*[\"'][^\"']*" + Regex.Escape(name), RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// The run of whitespace that begins the line containing <paramref name="index"/>, or the empty string when
    /// the close tag is not the first non-whitespace on its line (so we never copy stray non-whitespace).
    /// </summary>
    private static string LeadingWhitespaceOfLine(string html, int index)
    {
        var lineStart = html.LastIndexOf('\n', index > 0 ? index - 1 : 0);
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        var run = index - lineStart;
        for (var offset = 0; offset < run; offset++)
        {
            var character = html[lineStart + offset];
            if (character != ' ' && character != '\t')
            {
                return string.Empty;
            }
        }

        return html.Substring(lineStart, run);
    }
}
