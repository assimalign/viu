# Viu branding artifacts

These files are imported from the read-only `C:\Source\repos\assimalign\branding` repository. Regenerate branding there and copy only referenced Viu artifacts here; do not edit the imported artwork in this repository. The Visual Studio Code package directories carry byte-identical local copies because `vsce` cannot package files outside an extension root.

The transparent `svg/on-light/viu-mono-tight.svg` and `svg/on-dark/viu-mono-tight.svg` files are documented crops derived from the corresponding `svg/on-light/viu-mono.svg` and `svg/on-dark/viu-mono.svg` branding sources. The artwork, filters, and colors are unchanged; only the root `viewBox` is tightened from `0 0 96 96` to `18 15 60 73`. That box retains about four source units of margin around the rendered glyph bounds so the mark occupies more of Visual Studio Code's 16-pixel language-icon slot without being redrawn or raster-resampled.

Static single-asset surfaces use the opaque `nuget/png/viu-nuget-mono-light-*` artwork. Visual Studio's Extension Manager exposes only one icon and one preview image and cannot swap either asset with the active theme. The light tile therefore self-grounds the dark mono glyph so it remains legible on both light and dark UI; NuGet package icons, Visual Studio Code extension icons, and Marketplace imagery use the same static treatment for consistency.

Theme-aware editor surfaces use the transparent `png/on-light/viu-mono-*` and `png/on-dark/viu-mono-*` pair for fixed-size consumers and the tightly cropped SVG pair for scalable consumers. Visual Studio's Image Service selects the matching background PNG variant for `.viu` file icons, and Visual Studio Code selects the matching tightly cropped SVG language icon.
