


# Theme Enhancements for `.viu` files

> **Historical enhancement capture.** These observations record an earlier extension state and are
> retained as design history; they are not a current feature inventory. Explicit status labels note
> decisions that changed their scope.

1. [`Script Block`] C# Attribute Color needs to be the same color as in Visual Studio. Right now they show up kind golden. ![golden-color-attribute](../../assets/vs-enhancement-bad-attribute-color.png) Also property initializers should be white.
2. [`Script Block`] Property identifiers should be white. Currently they are the same color as the class. ![bad-property-color](../../assets/vs-enhancement-bad-property-color.png)
3. [`Script Block`] Property references also need to be standard visual studio white. ![alt text](../../assets/vs-enhancement-bad-property-reference-color.png)
4. [`Script Block`] namespace names in using statement need to be the standard white that is in Visual Studio ![alt text](../../assets/vs-enhancement-bad-using-statementcolor.png)
5. [`Script Block`] Parameters and variables need to be the standard light blue color that visual studios has for c#. ![alt text](../../assets/vs-enhancement-bad-params-variables-color.png)


# Template enhancements for `viu` files

**Intellisense**
1. [`Parked add-on history`] The former UtilityCss color-swatch request is retained for a future add-on redesign; utility completion is no longer a Viu or Visual Studio extension feature. The generic color-value completion transport remains dormant.
2. [`Template Block`] Event handler bindings should have intellisense within the template. Currently nothing shows up ![alt text](../../assets/vs-enhancement-event-handler-intellisense.png)
3. [`Template Block`] Template compiler is unable to differentiate between element tags and components that are of the same name. For example, having a template component called Button will render a `<button/>` element. ![alt text](../../assets/vs-enhancement-component-element-differentiation.png)
4. [`Delivered — Template/Style Block`] CSS classes defined in a component's `<style>` blocks are offered in template class-value IntelliSense.
5. [`Template Block`] Directives (`v-*`), Attribute Bindings (`:some-attribute="..."`), and Expression (`{{expression}}`) have no intellisense support. This needs to be added. ![alt text](../../assets/vs-enhancement-template-script-block-intellisense.png)
6. [`Script Block`] The `Assimalign.*` namespace no longer shows up in intellisense. ![alt text](../../assets/vs-enhancement-no-assimalign-namespace-in-intellisense.png)
7. [`Script/Template/Styles Block`] Intellisense should not showup in comment blocks. It shows up in all three blocks.

**Tags**
1. No design time errors or warning squiggle tags under any of the code.

**Hovering**
1. No info for any of the elements while hovering.
