# Generated server markup

`scripts/Test-EndToEnd.ps1` writes `index.html` into its isolated copy of this directory by
executing the packaged `EndToEndServerMarkup` fixture. That generator renders the exact shared
component tree through `ServerRenderAdaptor<TContext>` before the Browser SDK fixture is published.
No hand-authored hydration markers are accepted by the harness.
