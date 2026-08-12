using System;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Owns one text view's snippet session: opens it, moves between its fields, and ends it.
/// </summary>
/// <remarks>
/// <para>
/// A snippet session is what makes <c>prop</c> behave the way an author expects — Tab expands, Tab
/// moves to the next field, the last Tab or Enter commits — and only Visual Studio's expansion
/// service can open one. A completion item writes text and stops; the fields are the session's.
/// </para>
/// <para>
/// The interface is implemented rather than merely consumed because the expansion service calls back
/// during a session, and <see cref="EndExpansion"/> is how this type learns the session closed for
/// any reason — the author pressed Escape, committed the last field, or the view went away. Every
/// other callback is a notification Viu has nothing to add to.
/// </para>
/// </remarks>
internal sealed class ViuSnippetExpansionClient : IVsExpansionClient
{
    /// <summary>
    /// The language the snippets register under, matching the <c>Languages\CodeExpansions\Viu</c>
    /// entry in the extension's pkgdef.
    /// </summary>
    /// <remarks>
    /// Deliberately this extension's own identifier rather than a language service's. Registering a
    /// language service for <c>.viu</c> would attach it to the buffer and stamp its own content type,
    /// re-breaking the editor-factory ownership the pkgdef exists to establish; snippets need only a
    /// name and a search path under a GUID, and never a file-extension binding.
    /// </remarks>
    internal static readonly Guid LanguageGuid = new("4a1cf1b2-3d70-4e0e-9b2f-5a4d2f7f8f61");

    private readonly IVsTextView textView;
    private readonly IVsExpansionManager expansionManager;
    private readonly IVsExpansion expansion;
    private IVsExpansionSession? session;

    /// <summary>Initializes the client over one view's expansion surfaces.</summary>
    internal ViuSnippetExpansionClient(
        IVsTextView textView,
        IVsExpansionManager expansionManager,
        IVsExpansion expansion)
    {
        this.textView = textView ?? throw new ArgumentNullException(nameof(textView));
        this.expansionManager = expansionManager ??
            throw new ArgumentNullException(nameof(expansionManager));
        this.expansion = expansion ?? throw new ArgumentNullException(nameof(expansion));
    }

    /// <summary>Gets whether a session is open and therefore owns Tab, Enter, and Escape.</summary>
    internal bool IsSessionActive => this.session is not null;

    /// <summary>
    /// Expands a shortcut over the span it was typed in.
    /// </summary>
    /// <param name="shortcut">The shortcut word.</param>
    /// <param name="line">Zero-based line the shortcut sits on.</param>
    /// <param name="start">Zero-based offset the shortcut begins at.</param>
    /// <param name="end">Zero-based offset just past the shortcut.</param>
    /// <returns>Whether a session opened.</returns>
    internal bool TryExpand(string shortcut, int line, int start, int end)
    {
        var span = new TextSpan[1]
        {
            new()
            {
                iStartLine = line,
                iStartIndex = start,
                iEndLine = line,
                iEndIndex = end,
            },
        };

        Guid languageGuid = LanguageGuid;
        int found = this.expansionManager.GetExpansionByShortcut(
            this,
            languageGuid,
            shortcut,
            this.textView,
            span,
            0,
            out string path,
            out string title);
        if (found != VSConstants.S_OK || string.IsNullOrEmpty(path) || string.IsNullOrEmpty(title))
        {
            return false;
        }

        return this.expansion.InsertNamedExpansion(
            title,
            path,
            span[0],
            this,
            languageGuid,
            fShowDisambiguationUI: 0,
            pSession: out this.session) == VSConstants.S_OK &&
            this.session is not null;
    }

    /// <summary>Moves to the next field, ending the session after the last one.</summary>
    /// <returns>Whether the key was consumed by a session.</returns>
    internal bool TryGoToNextField()
        => this.session is { } current &&
           current.GoToNextExpansionField(0) == VSConstants.S_OK;

    /// <summary>Commits the session.</summary>
    /// <returns>Whether the key was consumed by a session.</returns>
    internal bool TryComplete()
        => this.session is { } current &&
           current.EndCurrentExpansion(0) == VSConstants.S_OK;

    /// <summary>Abandons the session, leaving the text it inserted in place.</summary>
    /// <returns>Whether the key was consumed by a session.</returns>
    internal bool TryCancel()
        => this.session is { } current &&
           current.EndCurrentExpansion(1) == VSConstants.S_OK;

    /// <inheritdoc />
    public int EndExpansion()
    {
        this.session = null;
        return VSConstants.S_OK;
    }

    /// <inheritdoc />
    public int IsValidKind(
        IVsTextLines buffer,
        TextSpan[] span,
        string bstrKind,
        out int isValidKind)
    {
        isValidKind = 1;
        return VSConstants.S_OK;
    }

    /// <inheritdoc />
    public int IsValidType(
        IVsTextLines buffer,
        TextSpan[] span,
        string[] types,
        int countTypes,
        out int isValidType)
    {
        isValidType = 1;
        return VSConstants.S_OK;
    }

    /// <inheritdoc />
    public int OnAfterInsertion(IVsExpansionSession expansionSession) => VSConstants.S_OK;

    /// <inheritdoc />
    public int OnBeforeInsertion(IVsExpansionSession expansionSession) => VSConstants.S_OK;

    /// <inheritdoc />
    public int OnItemChosen(string title, string path)
    {
        var span = new TextSpan[1];
        if (this.textView.GetCaretPos(out int line, out int column) != VSConstants.S_OK)
        {
            return VSConstants.E_FAIL;
        }

        span[0] = new TextSpan
        {
            iStartLine = line,
            iStartIndex = column,
            iEndLine = line,
            iEndIndex = column,
        };

        Guid languageGuid = LanguageGuid;
        return this.expansion.InsertNamedExpansion(
            title,
            path,
            span[0],
            this,
            languageGuid,
            fShowDisambiguationUI: 0,
            pSession: out this.session);
    }

    /// <inheritdoc />
    public int PositionCaretForEditing(IVsTextLines buffer, TextSpan[] span) => VSConstants.S_OK;

    /// <inheritdoc />
    public int GetExpansionFunction(
        MSXML.IXMLDOMNode xmlFunctionNode,
        string fieldName,
        out IVsExpansionFunction function)
    {
        // Viu ships no expansion functions; every field is a literal with a default.
        function = null!;
        return VSConstants.S_OK;
    }

    /// <inheritdoc />
    public int FormatSpan(IVsTextLines buffer, TextSpan[] span) => VSConstants.S_OK;
}
