using System;
using System.Threading;

namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// Emits Viu's independently maintained browser-normalization layer compatible with the
/// Tailwind CSS v4.3.3 Preflight behavior.
/// </summary>
/// <remarks>
/// The emitted CSS has no runtime dependency. Compatibility provenance and the upstream MIT
/// license are recorded in <c>docs/THIRD-PARTY-NOTICES.md</c>.
/// </remarks>
public static class UtilityPreflight
{
    private const string CssTemplate = """
*,
::after,
::before,
::backdrop,
::file-selector-button {
  box-sizing: border-box;
  margin: 0;
  padding: 0;
  border: 0 solid;
}

html,
:host {
  line-height: 1.5;
  -webkit-text-size-adjust: 100%;
  tab-size: 4;
  font-family: __DEFAULT_FONT_FAMILY__;
  font-feature-settings: __DEFAULT_FONT_FEATURE_SETTINGS__;
  font-variation-settings: __DEFAULT_FONT_VARIATION_SETTINGS__;
  -webkit-tap-highlight-color: transparent;
}

hr {
  height: 0;
  color: inherit;
  border-top-width: 1px;
}

abbr:where([title]) {
  -webkit-text-decoration: underline dotted;
  text-decoration: underline dotted;
}

h1,
h2,
h3,
h4,
h5,
h6 {
  font-size: inherit;
  font-weight: inherit;
}

a {
  color: inherit;
  -webkit-text-decoration: inherit;
  text-decoration: inherit;
}

b,
strong {
  font-weight: bolder;
}

code,
kbd,
samp,
pre {
  font-family: __DEFAULT_MONO_FONT_FAMILY__;
  font-feature-settings: __DEFAULT_MONO_FONT_FEATURE_SETTINGS__;
  font-variation-settings: __DEFAULT_MONO_FONT_VARIATION_SETTINGS__;
  font-size: 1em;
}

small {
  font-size: 80%;
}

sub,
sup {
  font-size: 75%;
  line-height: 0;
  position: relative;
  vertical-align: baseline;
}

sub {
  bottom: -0.25em;
}

sup {
  top: -0.5em;
}

table {
  text-indent: 0;
  border-color: inherit;
  border-collapse: collapse;
}

:-moz-focusring:where(:not(iframe)) {
  outline: auto;
}

progress {
  vertical-align: baseline;
}

summary {
  display: list-item;
}

ol,
ul,
menu {
  list-style: none;
}

img,
svg,
video,
canvas,
audio,
iframe,
embed,
object {
  display: block;
  vertical-align: middle;
}

img,
video {
  max-width: 100%;
  height: auto;
}

button,
input,
select,
optgroup,
textarea,
::file-selector-button {
  font: inherit;
  font-feature-settings: inherit;
  font-variation-settings: inherit;
  letter-spacing: inherit;
  color: inherit;
  border-radius: 0;
  background-color: transparent;
  opacity: 1;
}

:where(select:is([multiple], [size])) optgroup {
  font-weight: bolder;
}

:where(select:is([multiple], [size])) optgroup option {
  padding-inline-start: 20px;
}

::file-selector-button {
  margin-inline-end: 4px;
}

::placeholder {
  opacity: 1;
}

@supports (not (-webkit-appearance: -apple-pay-button)) or (contain-intrinsic-size: 1px) {
  ::placeholder {
    color: color-mix(in oklab, currentcolor 50%, transparent);
  }
}

textarea {
  resize: vertical;
}

::-webkit-search-decoration {
  -webkit-appearance: none;
}

::-webkit-date-and-time-value {
  min-height: 1lh;
  text-align: inherit;
}

::-webkit-datetime-edit {
  display: inline-flex;
}

::-webkit-datetime-edit-fields-wrapper {
  padding: 0;
}

::-webkit-datetime-edit,
::-webkit-datetime-edit-year-field,
::-webkit-datetime-edit-month-field,
::-webkit-datetime-edit-day-field,
::-webkit-datetime-edit-hour-field,
::-webkit-datetime-edit-minute-field,
::-webkit-datetime-edit-second-field,
::-webkit-datetime-edit-millisecond-field,
::-webkit-datetime-edit-meridiem-field {
  padding-block: 0;
}

::-webkit-calendar-picker-indicator {
  line-height: 1;
}

:-moz-ui-invalid {
  box-shadow: none;
}

button,
input:where([type='button'], [type='reset'], [type='submit']),
::file-selector-button {
  appearance: button;
}

::-webkit-inner-spin-button,
::-webkit-outer-spin-button {
  height: auto;
}

[hidden]:where(:not([hidden='until-found'])) {
  display: none !important;
}
""";

    private const string DefaultFontFamilyFallback =
        "-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', " +
        "'Noto Sans', Arial, sans-serif, 'Apple Color Emoji', 'Segoe UI Emoji', " +
        "'Segoe UI Symbol', 'Noto Color Emoji'";

    private const string DefaultMonoFontFamilyFallback =
        "ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono', " +
        "'Courier New', monospace";

    /// <summary>
    /// Gets Preflight rendered against <see cref="UtilityTheme.Default"/>.
    /// </summary>
    public static string DefaultCss { get; } =
        Emit(
            UtilityTheme.Default,
            CancellationToken.None);

    /// <summary>
    /// Renders the complete base normalization rules against <paramref name="theme"/>.
    /// </summary>
    /// <param name="theme">The theme supplying default proportional and monospace fonts.</param>
    /// <param name="cancellationToken">The build-host cancellation boundary.</param>
    /// <returns>Normalized CSS with LF line endings and a final newline.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="theme"/> is null.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    public static string Emit(
        UtilityTheme theme,
        CancellationToken cancellationToken)
    {
        if (theme is null)
        {
            throw new ArgumentNullException(nameof(theme));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var result = NormalizeLineEndings(CssTemplate);
        result = result.Replace(
            "__DEFAULT_FONT_FAMILY__",
            ResolveDefault(
                theme,
                "--default-font-family",
                DefaultFontFamilyFallback));
        cancellationToken.ThrowIfCancellationRequested();
        result = result.Replace(
            "__DEFAULT_FONT_FEATURE_SETTINGS__",
            ResolveDefault(
                theme,
                "--default-font-feature-settings",
                "normal"));
        result = result.Replace(
            "__DEFAULT_FONT_VARIATION_SETTINGS__",
            ResolveDefault(
                theme,
                "--default-font-variation-settings",
                "normal"));
        result = result.Replace(
            "__DEFAULT_MONO_FONT_FAMILY__",
            ResolveDefault(
                theme,
                "--default-mono-font-family",
                DefaultMonoFontFamilyFallback));
        cancellationToken.ThrowIfCancellationRequested();
        result = result.Replace(
            "__DEFAULT_MONO_FONT_FEATURE_SETTINGS__",
            ResolveDefault(
                theme,
                "--default-mono-font-feature-settings",
                "normal"));
        result = result.Replace(
            "__DEFAULT_MONO_FONT_VARIATION_SETTINGS__",
            ResolveDefault(
                theme,
                "--default-mono-font-variation-settings",
                "normal"));
        return result.EndsWith("\n", StringComparison.Ordinal)
            ? result
            : result + "\n";
    }

    private static string ResolveDefault(
        UtilityTheme theme,
        string propertyName,
        string fallback)
    {
        if (!theme.TryGetProperty(propertyName, out var property) ||
            property is null)
        {
            return fallback;
        }

        if ((property.Options & UtilityThemeOptions.Inline) != 0)
        {
            return property.Value;
        }

        return "var(" +
            theme.FormatCustomPropertyName(propertyName) +
            ", " +
            fallback +
            ")";
    }

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n").Replace('\r', '\n');
}
