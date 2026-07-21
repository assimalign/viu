# Assimalign.Viu.Forms

A registration form that exercises every implemented `v-model` flavor and modifier — the sample
gallery's showcase of Viu's form-binding surface. Vue reference:
[Form Input Bindings](https://vuejs.org/guide/essentials/forms.html).

## What it shows

- **`v-model` across input types** — text, email, number, `<textarea>`, a boolean checkbox, a checkbox
  group bound to a list, a radio group, and single + `multiple` `<select>`. Each is bound with the
  matching runtime directive (`_vModelText`, `_vModelCheckbox`, `_vModelRadio`, `_vModelSelect`)
  through `Directives.WithDirectives` and a `ViuModelBinding` (current value + write-back setter — no
  reflection, the AOT/trimming contract).
- **Modifiers** — `.trim` (name, email), `.number` (age arrives coerced to a number), and `.lazy`
  (the bio commits on change, not every keystroke).
- **Composition function + computed** — [`RegistrationForm`](Composition/RegistrationForm.cs) is the
  reactive model as a `ref`-per-field composition unit with `IsValid` and `Summary` computeds; it
  depends only on `Assimalign.Viu.Core` and is unit-tested with no browser.
- **A live view component** — [`FormPreviewComponent`](Components/FormPreviewComponent.cs) reflects the
  model as it changes and, because it uses no `v-model`, mounts in the in-memory test renderer.

All DOM writes flow through the injected node-ops adapter; there is no ad-hoc JS interop in the sample.

## Run it

```sh
dotnet run --project examples/Assimalign.Viu.Forms
```

Then open the served URL and edit the fields — the panel on the right updates live. Build a trimmed
publish (what CI's budget gate measures) with:

```sh
dotnet publish examples/Assimalign.Viu.Forms -c Release
```

## Tests

The reactive model and the preview component are covered by
[`Assimalign.Viu.Forms.Tests`](../Assimalign.Viu.Forms.Tests). The `v-model` DOM directives are a
browser concern (they require the DOM bridge) and are covered by the framework's own
`Assimalign.Viu.Browser` directive tests; here the tests pin the model and the derived state.
