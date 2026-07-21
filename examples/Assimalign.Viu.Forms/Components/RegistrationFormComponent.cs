using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

using Assimalign.Viu;
using Assimalign.Viu.Browser;

namespace Assimalign.Viu.Forms.Components;

/// <summary>
/// The registration form — the sample gallery's showcase of every implemented <c>v-model</c> flavor and
/// modifier. Each field is bound with the matching directive from <see cref="DomRenderHelpers"/> through
/// <see cref="Directives.WithDirectives(VirtualNode, IDirective, object?, string?, IReadOnlyDictionary{string, bool}?)"/>
/// and a <see cref="ViuModelBinding"/> (the current value plus a write-back setter — no reflection over
/// component members, the AOT/trimming contract). The live <see cref="FormPreviewComponent"/> reflects
/// the model as it changes. See https://vuejs.org/guide/essentials/forms.html.
/// <list type="bullet">
///   <item><description>text + <c>.trim</c> (<c>_vModelText</c>), email + <c>.trim</c></description></item>
///   <item><description>number + <c>.number</c> (edits arrive coerced to a number)</description></item>
///   <item><description>textarea + <c>.lazy</c> (commits on change, not every keystroke)</description></item>
///   <item><description>checkbox (boolean) and a checkbox group bound to a list</description></item>
///   <item><description>radio group and single/multiple <c>&lt;select&gt;</c></description></item>
/// </list>
/// </summary>
public sealed class RegistrationFormComponent : IComponent
{
    private static readonly FormPreviewComponent PreviewView = new();

    private static readonly string[] InterestOptions = ["Reactivity", "Rendering", "Tooling", "WASM"];
    private static readonly (string Value, string Label)[] ContactOptions =
        [("email", "Email"), ("phone", "Phone"), ("none", "Do not contact")];
    private static readonly (string Value, string Label)[] CountryOptions =
        [("", "Select a country…"), ("us", "United States"), ("gb", "United Kingdom"), ("de", "Germany"), ("jp", "Japan")];
    private static readonly (string Value, string Label)[] LanguageOptions =
        [("csharp", "C#"), ("fsharp", "F#"), ("ts", "TypeScript"), ("rust", "Rust")];

    /// <inheritdoc/>
    public string? Name => "RegistrationForm";

    /// <inheritdoc/>
    public ComponentSetup Setup(ComponentProperties properties, ComponentSetupContext context)
    {
        var form = new RegistrationForm();

        return () => VirtualNodeFactory.Element(
            "section",
            VirtualNodeFactory.Properties(("class", "shell")),
            VirtualNodeFactory.Element(
                "form",
                VirtualNodeFactory.Properties(
                    ("class", "form"),
                    // A no-op submit handler with the .prevent modifier so pressing Enter never reloads.
                    ("onSubmit", DomRenderHelpers._withModifiers((Action)(() => { }), "prevent"))),
                VirtualNodeFactory.Element("h1", "Create your account"),

                // text + .trim
                Field("name", "Full name", Model(
                    Control("name", "text"),
                    DomRenderHelpers._vModelText,
                    form.FullName.Value,
                    value => form.FullName.Value = AsString(value),
                    "trim")),

                // email + .trim
                Field("email", "Email", Model(
                    Control("email", "email"),
                    DomRenderHelpers._vModelText,
                    form.Email.Value,
                    value => form.Email.Value = AsString(value),
                    "trim")),

                // number + .number
                Field("age", "Age", Model(
                    Control("age", "number"),
                    DomRenderHelpers._vModelText,
                    form.Age.Value,
                    value => form.Age.Value = AsDouble(value),
                    "number")),

                // textarea + .lazy
                Field("bio", "Bio", Model(
                    VirtualNodeFactory.Element(
                        "textarea",
                        VirtualNodeFactory.Properties(("id", "bio"), ("class", "control"), ("rows", 3)),
                        (VirtualNode?[]?)null),
                    DomRenderHelpers._vModelText,
                    form.Bio.Value,
                    value => form.Bio.Value = AsString(value),
                    "lazy")),

                // checkbox (boolean)
                VirtualNodeFactory.Element(
                    "div",
                    VirtualNodeFactory.Properties(("class", "field field-inline")),
                    Model(
                        VirtualNodeFactory.Element(
                            "input",
                            VirtualNodeFactory.Properties(("id", "terms"), ("type", "checkbox")),
                            (VirtualNode?[]?)null),
                        DomRenderHelpers._vModelCheckbox,
                        form.AcceptsTerms.Value,
                        value => form.AcceptsTerms.Value = AsBool(value)),
                    VirtualNodeFactory.Element(
                        "label",
                        VirtualNodeFactory.Properties(("for", "terms")),
                        "I accept the terms")),

                // checkbox group bound to a list
                Field("interests", "Interests", InterestGroup(form)),

                // radio group
                Field("contact", "Preferred contact", ContactGroup(form)),

                // select (single)
                Field("country", "Country", Model(
                    VirtualNodeFactory.Element(
                        "select",
                        VirtualNodeFactory.Properties(("id", "country"), ("class", "control")),
                        BuildOptions(CountryOptions)),
                    DomRenderHelpers._vModelSelect,
                    form.Country.Value,
                    value => form.Country.Value = AsString(value))),

                // select (multiple)
                Field("languages", "Languages", Model(
                    VirtualNodeFactory.Element(
                        "select",
                        VirtualNodeFactory.Properties(("id", "languages"), ("class", "control"), ("multiple", true)),
                        BuildOptions(LanguageOptions)),
                    DomRenderHelpers._vModelSelect,
                    form.Languages.Value,
                    value => form.Languages.Value = AsList(value)))),

            VirtualNodeFactory.Component(PreviewView, VirtualNodeFactory.Properties(("form", form))));
    }

    // --- v-model helper -------------------------------------------------------------------------

    // Attaches a v-model directive to an input, carrying the current value + write-back setter and the
    // requested modifiers (upstream: withDirectives(vnode, [[dir, value, arg, modifiers]])).
    private static VirtualNode Model(
        VirtualNode input,
        IDirective directive,
        object? value,
        Action<object?> setter,
        params string[] modifiers)
    {
        IReadOnlyDictionary<string, bool>? modifierMap = null;
        if (modifiers.Length > 0)
        {
            var map = new Dictionary<string, bool>(modifiers.Length, StringComparer.Ordinal);
            foreach (var modifier in modifiers)
            {
                map[modifier] = true;
            }
            modifierMap = map;
        }
        return Directives.WithDirectives(input, directive, new ViuModelBinding(value, setter), null, modifierMap);
    }

    // --- element helpers ------------------------------------------------------------------------

    private static VirtualNode Control(string id, string type) => VirtualNodeFactory.Element(
        "input",
        VirtualNodeFactory.Properties(("id", id), ("type", type), ("class", "control")),
        (VirtualNode?[]?)null);

    private static VirtualNode Field(string forId, string label, VirtualNode control) => VirtualNodeFactory.Element(
        "div",
        VirtualNodeFactory.Properties(("class", "field")),
        VirtualNodeFactory.Element("label", VirtualNodeFactory.Properties(("for", forId)), label),
        control);

    private static VirtualNode[] BuildOptions((string Value, string Label)[] options)
    {
        var nodes = new VirtualNode[options.Length];
        for (var index = 0; index < options.Length; index++)
        {
            var (value, label) = options[index];
            nodes[index] = VirtualNodeFactory.Element(
                "option",
                VirtualNodeFactory.Properties(("value", value)),
                label);
        }
        return nodes;
    }

    private static VirtualNode InterestGroup(RegistrationForm form)
    {
        var boxes = new VirtualNode[InterestOptions.Length];
        for (var index = 0; index < InterestOptions.Length; index++)
        {
            var interest = InterestOptions[index];
            boxes[index] = VirtualNodeFactory.Element(
                "label",
                VirtualNodeFactory.Properties(("class", "option")),
                Model(
                    VirtualNodeFactory.Element(
                        "input",
                        VirtualNodeFactory.Properties(("type", "checkbox"), ("value", interest)),
                        (VirtualNode?[]?)null),
                    DomRenderHelpers._vModelCheckbox,
                    form.Interests.Value,
                    value => form.Interests.Value = AsList(value)),
                VirtualNodeFactory.Text(" " + interest));
        }
        return VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("class", "options")), boxes);
    }

    private static VirtualNode ContactGroup(RegistrationForm form)
    {
        var radios = new VirtualNode[ContactOptions.Length];
        for (var index = 0; index < ContactOptions.Length; index++)
        {
            var (value, label) = ContactOptions[index];
            radios[index] = VirtualNodeFactory.Element(
                "label",
                VirtualNodeFactory.Properties(("class", "option")),
                Model(
                    VirtualNodeFactory.Element(
                        "input",
                        VirtualNodeFactory.Properties(("type", "radio"), ("name", "contact"), ("value", value)),
                        (VirtualNode?[]?)null),
                    DomRenderHelpers._vModelRadio,
                    form.ContactMethod.Value,
                    selected => form.ContactMethod.Value = AsString(selected)),
                VirtualNodeFactory.Text(" " + label));
        }
        return VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("class", "options")), radios);
    }

    // --- value coercion -------------------------------------------------------------------------

    private static string AsString(object? value) => value as string ?? value?.ToString() ?? string.Empty;

    private static bool AsBool(object? value) => value is bool boolean && boolean;

    private static IList AsList(object? value) => value as IList ?? new List<string>();

    private static double AsDouble(object? value) => value switch
    {
        double number => number,
        int integer => integer,
        string text when double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => 0d,
    };
}
