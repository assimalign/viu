using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.Forms;

/// <summary>
/// The reactive model behind the registration form, written as a composition function: one
/// <c>Reference&lt;T&gt;</c> per field (Vue's <c>ref()</c>, the natural pairing for <c>v-model</c>)
/// plus a couple of <c>Computed</c> derivations. It depends only on <c>Assimalign.Viu.Reactivity</c>,
/// so the sibling test project exercises the field bindings and the derived state with no browser.
/// The form component binds each field with the matching <c>v-model</c> directive; see
/// https://vuejs.org/guide/essentials/forms.html.
/// </summary>
public sealed class RegistrationForm
{
    /// <summary>The full-name text field (bound with <c>.trim</c>).</summary>
    public Reference<string> FullName { get; } = Reactive.Reference(string.Empty);

    /// <summary>The email text field (bound with <c>.trim</c>).</summary>
    public Reference<string> Email { get; } = Reactive.Reference(string.Empty);

    /// <summary>The age number field (bound with <c>.number</c>, so edits arrive as a number).</summary>
    public Reference<double> Age { get; } = Reactive.Reference(0d);

    /// <summary>The multi-line bio (a <c>&lt;textarea&gt;</c> bound with <c>.lazy</c>).</summary>
    public Reference<string> Bio { get; } = Reactive.Reference(string.Empty);

    /// <summary>The terms checkbox (a single boolean <c>v-model</c>).</summary>
    public Reference<bool> AcceptsTerms { get; } = Reactive.Reference(false);

    /// <summary>The preferred contact method (a radio group).</summary>
    public Reference<string> ContactMethod { get; } = Reactive.Reference("email");

    /// <summary>The country (a single <c>&lt;select&gt;</c>).</summary>
    public Reference<string> Country { get; } = Reactive.Reference(string.Empty);

    /// <summary>The chosen interests (a checkbox group bound to a list — Vue's array checkbox model).</summary>
    public Reference<IList> Interests { get; } = Reactive.Reference<IList>(new List<string>());

    /// <summary>The known languages (a <c>&lt;select multiple&gt;</c> bound to a list).</summary>
    public Reference<IList> Languages { get; } = Reactive.Reference<IList>(new List<string>());

    /// <summary>Whether the form may be submitted — a name, an <c>@</c>-bearing email, and accepted terms.</summary>
    public Computed<bool> IsValid { get; }

    /// <summary>A single-line, live summary of the whole model — recomputes on any field change.</summary>
    public Computed<string> Summary { get; }

    /// <summary>Creates an empty form model with its derived state wired up.</summary>
    public RegistrationForm()
    {
        IsValid = Reactive.Computed(() =>
            !string.IsNullOrWhiteSpace(FullName.Value)
            && Email.Value.Contains('@')
            && AcceptsTerms.Value);

        Summary = Reactive.Computed(() =>
        {
            var name = string.IsNullOrWhiteSpace(FullName.Value) ? "(no name)" : FullName.Value;
            var age = Age.Value.ToString("0.##", CultureInfo.InvariantCulture);
            var interests = Join(Interests.Value);
            var languages = Join(Languages.Value);
            return $"{name} <{Email.Value}>, age {age}; contact by {ContactMethod.Value}; "
                + $"country {Emptyable(Country.Value)}; interests [{interests}]; languages [{languages}]; "
                + $"terms {(AcceptsTerms.Value ? "accepted" : "not accepted")}";
        });
    }

    private static string Emptyable(string value) => string.IsNullOrEmpty(value) ? "(none)" : value;

    private static string Join(IList list) => string.Join(", ", list.Cast<object?>().Select(item => item?.ToString()));
}
