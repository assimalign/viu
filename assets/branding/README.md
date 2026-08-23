# Viu branding artifacts

These files are imported from the read-only `C:\Source\repos\assimalign\branding` repository. Regenerate branding there and copy only referenced Viu artifacts here; do not edit the imported images in this repository. The Visual Studio Code package directories carry byte-identical local copies because `vsce` cannot package files outside an extension root.

Static single-asset surfaces use the opaque `nuget/png/viu-nuget-mono-light-*` artwork. Visual Studio's Extension Manager exposes only one icon and one preview image and cannot swap either asset with the active theme. The light tile therefore self-grounds the dark mono glyph so it remains legible on both light and dark UI; NuGet package icons, Visual Studio Code extension icons, and Marketplace imagery use the same static treatment for consistency.

Theme-aware editor surfaces use the transparent `png/on-light/viu-mono-*` and `png/on-dark/viu-mono-*` pair. Visual Studio's Image Service selects the matching background variant for `.viu` file icons, and Visual Studio Code selects the matching language icon.
