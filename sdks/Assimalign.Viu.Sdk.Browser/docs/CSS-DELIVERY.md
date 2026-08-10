# Browser SDK CSS delivery

`Assimalign.Viu.Sdk.Browser` owns the delivery of CSS produced by Viu projects. The compiler and
base SDK produce deterministic component styles; the Browser SDK turns those outputs into browser
static assets and links them from the host page during both `Build` and `Publish`.

## Delivered stylesheets

A Browser application can receive two component-style sources:

- its own `<PackageId>.viu.css` bundle; and
- one `<PackageId>.viu.css` bundle from each referenced component-library package, carried by that
  package's base-SDK `buildTransitive` registration.

The Browser SDK copies library bundles to
`_content/<PackageId>/<PackageId>.viu.css`. It links referenced-library bundles first in ordinal
route order, then links the application bundle. The application therefore retains the last cascade
position. All missing links are inserted in one host-page read/write pass, and an existing `href`
suppresses only its matching generated link. A file name mentioned in text or an HTML comment does
not suppress injection.

The injection is independent of `OverrideHtmlAssetPlaceholders`. When that WebAssembly property is
`true`, Viu runs after the SDK's boot-placeholder rewrite; when it is `false`, Viu reads the resolved
host-page static asset directly. In both cases Viu writes an intermediate copy, re-registers its
static-web-asset identity and integrity, and completes before compression. The published
`index.html`, `index.html.gz`, and `index.html.br` therefore contain the same stylesheet links.

## Routes and fingerprinting

The application bundle uses the optional static-web-asset fingerprint expression
`<PackageId>#[.{fingerprint}]?.viu.css`. This registers both a stable plain route and an immutable
fingerprinted endpoint labeled `<PackageId>.viu.css`.

The generated link uses the plain route by default. This is the safe default for a standalone static
publish because `<PackageId>.viu.css` is the physical file in `wwwroot`. A manifest-aware server or
CDN can opt into the immutable route:

```xml
<PropertyGroup>
    <ViuUseFingerprintedSingleFileComponentCssBundleLink>true</ViuUseFingerprintedSingleFileComponentCssBundleLink>
</PropertyGroup>
```

The Browser SDK resolves the actual route from the static-web-asset endpoint whose `label` is the
plain bundle name and which carries `fingerprint` metadata. It never constructs or guesses the hash.
The opt-in is `false` by default. If a deployment owns a different route policy, an explicit value
has the highest precedence:

```xml
<PropertyGroup>
    <ViuUseFingerprintedSingleFileComponentCssBundleLink>true</ViuUseFingerprintedSingleFileComponentCssBundleLink>
    <ViuSingleFileComponentCssBundleLinkHref>assets/application.css</ViuSingleFileComponentCssBundleLinkHref>
</PropertyGroup>
```

Here the explicit `assets/application.css` wins and endpoint lookup is skipped.

## Switches

| Property | Default | Effect |
| --- | --- | --- |
| `ViuBundleSingleFileComponentCss` | `ViuUseSingleFileComponents` | Produces and registers the application's component bundle. Component libraries use the same switch during pack. |
| `ViuInjectSingleFileComponentCssLink` | `true` for Browser applications | Injects all referenced-library links and the application link when present. Set `false` only when the host page owns every link. |
| `ViuUseFingerprintedSingleFileComponentCssBundleLink` | `false` | Resolves the application href from its labeled fingerprinted endpoint. Requires a manifest-aware deployment. |
| `ViuSingleFileComponentCssBundleLinkHref` | `<PackageId>.viu.css` | Explicit application href. When authored, it overrides the fingerprint opt-in. |
| `OverrideHtmlAssetPlaceholders` | WebAssembly SDK policy | Controls boot-placeholder rewriting only; it does not enable or disable Viu CSS delivery. |

`ViuBundleUtilityCss` is independent. Its `<PackageId>.utilities.css` link uses the same
compression-safe host-page registration path, but it does not change component bundle ordering or
fingerprint policy.

This contract implements [V01.01.12.12.01], [V01.01.12.12.03], [V01.01.12.12.04],
[V01.01.12.12.05], [V01.01.12.12.06], and [V01.01.12.12.07].
