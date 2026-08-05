using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Renders the editor state <see cref="ViuEditorDiagnostics"/> traces ([V01.01.12.07.09]).
/// </summary>
/// <remarks>
/// <para>
/// Split from the sink because these touch Visual Studio types and the sink deliberately does not:
/// the sink is source-linked into the test project, this is not. Every method here is called only
/// from inside a <see cref="ViuEditorDiagnostics.Trace"/> message factory, which is where the guard
/// against a throw lives.
/// </para>
/// <para>
/// <b>Reflection, deliberately and narrowly.</b> <see cref="DescribeBraceCompletionManager"/> reads
/// the editor's own brace-completion manager — the object that decides whether a session starts —
/// out of the view's property collection under the string key the editor stores it as, and reports
/// four of its properties. That type is internal to <c>Microsoft.VisualStudio.Platform.VSEditor</c>
/// and has no public contract, and its live view of the option and of the registered brace
/// characters is exactly the evidence this investigation is missing. This is the one place in the
/// repository that reflects: it is diagnostics-only, dormant unless the environment variable is set,
/// and it runs in a .NET Framework in-process Visual Studio extension that is never trimmed and
/// never ahead-of-time compiled — so the repository's AOT and trimming constraints, which govern the
/// shipping WebAssembly runtime, are not in play. A missing member degrades to <c>&lt;absent&gt;</c>
/// rather than throwing.
/// </para>
/// </remarks>
internal static class ViuEditorDiagnosticsDescriptions
{
    private const string BraceCompletionManagerPropertyKey = "BraceCompletionManager";

    /// <summary>
    /// Renders a content type with every base definition it transitively derives from.
    /// </summary>
    /// <param name="contentType">The content type to render.</param>
    /// <returns>The type name followed by its full, de-duplicated base chain.</returns>
    public static string DescribeContentType(IContentType? contentType)
    {
        if (contentType is null)
        {
            return "<null>";
        }

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<string> bases = [];
        CollectBaseTypes(contentType, seen, bases);

        return string.Concat(contentType.TypeName, " bases=[", string.Join(",", bases.ToArray()), "]");
    }

    /// <summary>
    /// Renders a view's role set.
    /// </summary>
    /// <param name="roles">The roles to render.</param>
    /// <returns>The role names, comma separated.</returns>
    public static string DescribeRoles(ITextViewRoleSet? roles)
    {
        if (roles is null)
        {
            return "<null>";
        }

        List<string> names = [];
        foreach (string role in roles)
        {
            names.Add(role);
        }

        names.Sort(StringComparer.Ordinal);
        return string.Concat("[", string.Join(",", names.ToArray()), "]");
    }

    /// <summary>
    /// Renders the Automatic Brace Completion option as the given view sees it.
    /// </summary>
    /// <param name="options">The view's options.</param>
    /// <returns>
    /// The effective value, plus whether it is defined in the view's own scope or only inherited from
    /// the global scope — which is what separates "nobody ever set it" from "somebody set it off".
    /// </returns>
    public static string DescribeBraceCompletionOption(IEditorOptions? options)
    {
        if (options is null)
        {
            return "<null>";
        }

        string effective = ReadOption(options, localScopeOnly: false);
        string local = TryDescribeIsDefined(options, localScopeOnly: true);
        string global = options.GlobalOptions is { } globalOptions
            ? ReadOption(globalOptions, localScopeOnly: false)
            : "<no global scope>";

        return string.Concat(
            "effective=", effective,
            " definedOnThisView=", local,
            " globalValue=", global);
    }

    /// <summary>
    /// Renders the editor's brace-completion manager for a view, if it has been created yet.
    /// </summary>
    /// <param name="textView">The view to read.</param>
    /// <returns>
    /// Whether the manager exists, whether it considers brace completion enabled, how many sessions
    /// are active, and the opening and closing characters its aggregator resolved for this view.
    /// </returns>
    public static string DescribeBraceCompletionManager(ITextView? textView)
    {
        if (textView is null)
        {
            return "<null view>";
        }

        object? manager = null;
        try
        {
            if (!textView.Properties.TryGetProperty(BraceCompletionManagerPropertyKey, out manager) ||
                manager is null)
            {
                // Created lazily on the first command routed through the view, so "absent" at view
                // creation is expected and only interesting if it is still absent while typing.
                return "present=False";
            }
        }
        catch (Exception exception)
        {
            return "present=<threw " + exception.GetType().Name + ">";
        }

        return string.Concat(
            "present=True type=", manager.GetType().FullName,
            " enabled=", ReadPropertyText(manager, "Enabled"),
            " activeSessions=", ReadPropertyText(manager, "ActiveSessionCount"),
            " openingBraces=", ViuEditorDiagnostics.Describe(ReadPropertyText(manager, "OpeningBraces")),
            " closingBraces=", ViuEditorDiagnostics.Describe(ReadPropertyText(manager, "ClosingBraces")));
    }

    /// <summary>
    /// Renders a position in the buffer as a line and column pair plus its container section.
    /// </summary>
    /// <param name="snapshot">The snapshot the position belongs to.</param>
    /// <param name="position">The character position.</param>
    /// <returns>The one-based line and column, and the section the scanner attributes that line to.</returns>
    public static string DescribePosition(ITextSnapshot snapshot, int position)
    {
        ITextSnapshotLine line = snapshot.GetLineFromPosition(position);
        int column = position - line.Start.Position;

        ViuSectionKind section;
        try
        {
            section = ViuSectionScanner.ScanLineSections(ViuSnapshotLines.Read(snapshot))[line.LineNumber];
        }
        catch (Exception exception)
        {
            return string.Concat(
                "line=", (line.LineNumber + 1).ToString(CultureInfo.InvariantCulture),
                " column=", (column + 1).ToString(CultureInfo.InvariantCulture),
                " section=<threw ", exception.GetType().Name, ">");
        }

        return string.Concat(
            "line=", (line.LineNumber + 1).ToString(CultureInfo.InvariantCulture),
            " column=", (column + 1).ToString(CultureInfo.InvariantCulture),
            " section=", section.ToString(),
            " lineText=", ViuEditorDiagnostics.Describe(line.GetText()));
    }

    /// <summary>
    /// Renders one text change as a position, a deleted length, and the inserted text.
    /// </summary>
    /// <param name="changes">The changes of one buffer edit.</param>
    /// <returns>A single-line rendering of every change in the edit.</returns>
    public static string DescribeChanges(INormalizedTextChangeCollection changes)
    {
        StringBuilder builder = new();
        for (int index = 0; index < changes.Count; index++)
        {
            ITextChange change = changes[index];
            if (index > 0)
            {
                builder.Append(' ');
            }

            builder
                .Append("[at=").Append(change.OldPosition.ToString(CultureInfo.InvariantCulture))
                .Append(" deleted=").Append(ViuEditorDiagnostics.Describe(change.OldText))
                .Append(" inserted=").Append(ViuEditorDiagnostics.Describe(change.NewText))
                .Append(']');
        }

        return builder.ToString();
    }

    private static void CollectBaseTypes(IContentType contentType, HashSet<string> seen, List<string> bases)
    {
        foreach (IContentType baseType in contentType.BaseTypes)
        {
            if (!seen.Add(baseType.TypeName))
            {
                continue;
            }

            bases.Add(baseType.TypeName);
            CollectBaseTypes(baseType, seen, bases);
        }
    }

    private static string ReadOption(IEditorOptions options, bool localScopeOnly)
    {
        try
        {
            return options.GetOptionValue(DefaultTextViewOptions.BraceCompletionEnabledOptionId)
                .ToString(CultureInfo.InvariantCulture);
        }
        catch (Exception exception)
        {
            return "<threw " + exception.GetType().Name + ">";
        }
    }

    private static string TryDescribeIsDefined(IEditorOptions options, bool localScopeOnly)
    {
        try
        {
            return options
                .IsOptionDefined(DefaultTextViewOptions.BraceCompletionEnabledOptionId, localScopeOnly)
                .ToString(CultureInfo.InvariantCulture);
        }
        catch (Exception exception)
        {
            return "<threw " + exception.GetType().Name + ">";
        }
    }

    private static string ReadPropertyText(object instance, string propertyName)
    {
        try
        {
            PropertyInfo? property = instance
                .GetType()
                .GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property is null)
            {
                return "<absent>";
            }

            object? value = property.GetValue(instance, null);
            return value?.ToString() ?? "<null>";
        }
        catch (Exception exception)
        {
            return "<threw " + exception.GetType().Name + ">";
        }
    }
}
