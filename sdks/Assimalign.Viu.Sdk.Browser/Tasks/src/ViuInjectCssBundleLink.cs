using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Assimalign.Viu.Sdk.Browser.Tasks;

/// <summary>
/// The MSBuild task that writes a copy of a WebAssembly app's host page (<c>wwwroot/index.html</c>) with
/// deterministic <c>&lt;link rel="stylesheet"&gt;</c> references to the application's and its referenced
/// component libraries' bundled <c>.viu</c> stylesheets spliced into its <c>&lt;head&gt;</c> — so a consuming
/// app needs no hand-authored link tags ([V01.01.12.12.01], [V01.01.12.12.06]). The paired targets in
/// <c>build/Targets/Build.Css.Bundling.targets</c> re-register the written copy as a
/// <c>StaticWebAsset</c> (recomputing its integrity and endpoints) <b>before</b> the SDK's compression
/// pipeline runs, so gzip/brotli variants are derived from the injected content and negotiation is never
/// desynced — the failure mode that made an earlier in-place transformation drop compressed variants.
/// <para>
/// This task only <em>reads</em> the current host page and <em>writes</em> a transformed copy to an
/// intermediate path; it never edits the source file in place. Splicing is idempotent per stylesheet: an
/// existing hand-authored reference suppresses only that reference, while every missing bundle is still
/// injected. The document's newline style and the closing tag's indentation are preserved. This is a
/// build-tooling text transform — it does not parse HTML — so it deliberately matches only the literal
/// <c>&lt;/head&gt;</c> close tag. Referenced-library links sort ordinally before the application link; the
/// application link therefore retains the final cascade position.
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
    /// Gets or sets the stylesheet links to inject in one read/transform/write pass. Each item uses its
    /// <c>Href</c> metadata, falling back to its item specification; <c>BundleFileName</c> scopes the
    /// idempotency check; and the integer <c>Order</c> metadata groups links before ordinal href ordering.
    /// An optional <c>FingerprintLabel</c> requests resolution from <see cref="StaticWebAssetEndpoints"/>.
    /// Specified by [V01.01.12.12.06].
    /// </summary>
    public ITaskItem[] StylesheetLinks { get; set; } = Array.Empty<ITaskItem>();

    /// <summary>
    /// Gets or sets the static-web-asset endpoints used to resolve a link whose
    /// <c>FingerprintLabel</c> metadata is present. The matching endpoint must carry both a
    /// <c>label</c> property equal to that value and a <c>fingerprint</c> property; its item specification is
    /// the immutable route injected for a manifest-aware host.
    /// Specified by [V01.01.12.12.04].
    /// </summary>
    public ITaskItem[] StaticWebAssetEndpoints { get; set; } = Array.Empty<ITaskItem>();

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

        if (!TryResolveStylesheetLinks(out var links))
        {
            return false;
        }

        var transformed = html;
        var injected = false;
        foreach (var link in links)
        {
            transformed = CssBundleLinkInjector.InjectStylesheetLink(
                transformed,
                link.Href,
                link.BundleFileName,
                out var currentLinkInjected);
            injected |= currentLinkInjected;
        }

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
                ? "ViuInjectCssBundleLink: injected missing stylesheet links into {0}."
                : "ViuInjectCssBundleLink: every stylesheet was already referenced, or the page has no </head>; {0} written unchanged.",
            OutputPath);
        return true;
    }

    private bool TryResolveStylesheetLinks(out List<StylesheetLink> links)
    {
        links = new List<StylesheetLink>();
        var hrefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in StylesheetLinks)
        {
            if (item is null)
            {
                continue;
            }

            var href = item.GetMetadata("Href");
            if (string.IsNullOrEmpty(href))
            {
                href = item.ItemSpec;
            }

            var fingerprintLabel = item.GetMetadata("FingerprintLabel");
            if (!string.IsNullOrEmpty(fingerprintLabel) &&
                !TryResolveFingerprintedHref(
                    StaticWebAssetEndpoints,
                    fingerprintLabel,
                    out href))
            {
                Log.LogError(
                    "ViuInjectCssBundleLink: no fingerprinted static-web-asset endpoint is labeled '{0}'. " +
                    "Enable static-web-asset fingerprinting, disable the fingerprinted CSS-link opt-in, " +
                    "or set ViuSingleFileComponentCssBundleLinkHref explicitly.",
                    fingerprintLabel);
                return false;
            }

            if (string.IsNullOrEmpty(href) || !hrefs.Add(href))
            {
                continue;
            }

            var bundleFileName = item.GetMetadata("BundleFileName");
            var order = 0;
            _ = int.TryParse(item.GetMetadata("Order"), out order);
            links.Add(new StylesheetLink(href, bundleFileName, order));
        }

        links.Sort(StylesheetLinkComparer.Instance);
        return true;
    }

    internal static bool TryResolveFingerprintedHref(
        ITaskItem[] endpoints,
        string label,
        out string href)
    {
        href = string.Empty;
        if (endpoints is null || string.IsNullOrEmpty(label))
        {
            return false;
        }

        foreach (var endpoint in endpoints)
        {
            if (endpoint is null)
            {
                continue;
            }

            var properties = endpoint.GetMetadata("EndpointProperties");
            if (EndpointPropertyEquals(properties, "label", label) &&
                EndpointPropertyExists(properties, "fingerprint"))
            {
                href = endpoint.ItemSpec;
                return !string.IsNullOrEmpty(href);
            }
        }

        return false;
    }

    private static bool EndpointPropertyEquals(string properties, string name, string value)
    {
        if (string.IsNullOrEmpty(properties))
        {
            return false;
        }

        var pattern =
            "\\{\\s*\"Name\"\\s*:\\s*\"" + Regex.Escape(name) +
            "\"\\s*,\\s*\"Value\"\\s*:\\s*\"" + Regex.Escape(value) + "\"\\s*\\}";
        return Regex.IsMatch(properties, pattern, RegexOptions.IgnoreCase);
    }

    private static bool EndpointPropertyExists(string properties, string name)
    {
        if (string.IsNullOrEmpty(properties))
        {
            return false;
        }

        var pattern =
            "\\{\\s*\"Name\"\\s*:\\s*\"" + Regex.Escape(name) +
            "\"\\s*,\\s*\"Value\"\\s*:\\s*\"[^\"]+\"\\s*\\}";
        return Regex.IsMatch(properties, pattern, RegexOptions.IgnoreCase);
    }

    private readonly struct StylesheetLink
    {
        internal StylesheetLink(string href, string bundleFileName, int order)
        {
            Href = href;
            BundleFileName = bundleFileName;
            Order = order;
        }

        internal string Href { get; }

        internal string BundleFileName { get; }

        internal int Order { get; }
    }

    private sealed class StylesheetLinkComparer : IComparer<StylesheetLink>
    {
        internal static StylesheetLinkComparer Instance { get; } = new StylesheetLinkComparer();

        public int Compare(StylesheetLink x, StylesheetLink y)
        {
            var order = x.Order.CompareTo(y.Order);
            return order != 0
                ? order
                : string.Compare(x.Href, y.Href, StringComparison.Ordinal);
        }
    }
}
