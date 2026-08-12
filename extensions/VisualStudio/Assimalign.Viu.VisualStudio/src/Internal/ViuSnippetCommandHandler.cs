using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Expands a snippet shortcut on <c>Tab</c>, and gives <c>Tab</c>, <c>Enter</c>, and <c>Escape</c> to
/// the session while one is open.
/// </summary>
/// <remarks>
/// <para>
/// A thin adapter over <see cref="ViuSnippetShortcut"/> and
/// <see cref="ViuSnippetExpansionClient"/>: every rule about <em>when</em> a key means expansion
/// lives in the first and is unit-tested there, and the session itself belongs to the second.
/// </para>
/// <para>
/// <b>The keys are the author's by default.</b> Each handler reports the command unhandled unless a
/// session is open or a shipped shortcut sits immediately before the caret, so Tab keeps indenting
/// and Enter keeps inserting a line everywhere else. Tab also commits a completion list, which is
/// why an open completion session is checked first: a shortcut and a completion item can both be
/// waiting on the same keystroke, and the list is what the author is looking at.
/// </para>
/// </remarks>
[Export(typeof(ICommandHandler))]
[Name(nameof(ViuSnippetCommandHandler))]
[ContentType(ViuContentTypes.Viu)]
[TextViewRole(PredefinedTextViewRoles.Editable)]
internal sealed class ViuSnippetCommandHandler :
    ICommandHandler<TabKeyCommandArgs>,
    ICommandHandler<ReturnKeyCommandArgs>,
    ICommandHandler<EscapeKeyCommandArgs>
{
    private static readonly object ClientPropertyKey = new();

    private readonly IVsEditorAdaptersFactoryService adapterFactory;
    private readonly IAsyncCompletionBroker completionBroker;
    private readonly IServiceProvider serviceProvider;

    /// <summary>Initializes the handler from the editor and shell services Visual Studio supplies.</summary>
    [ImportingConstructor]
    internal ViuSnippetCommandHandler(
        IVsEditorAdaptersFactoryService adapterFactory,
        IAsyncCompletionBroker completionBroker,
        [Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider)
    {
        this.adapterFactory = adapterFactory ?? throw new ArgumentNullException(nameof(adapterFactory));
        this.completionBroker = completionBroker ??
            throw new ArgumentNullException(nameof(completionBroker));
        this.serviceProvider = serviceProvider ??
            throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <inheritdoc />
    public string DisplayName => "Viu snippet expansion";

    /// <inheritdoc />
    public CommandState GetCommandState(TabKeyCommandArgs args) => CommandState.Unspecified;

    /// <inheritdoc />
    public CommandState GetCommandState(ReturnKeyCommandArgs args) => CommandState.Unspecified;

    /// <inheritdoc />
    public CommandState GetCommandState(EscapeKeyCommandArgs args) => CommandState.Unspecified;

    /// <inheritdoc />
    public bool ExecuteCommand(TabKeyCommandArgs args, CommandExecutionContext context)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ViuSnippetExpansionClient? client = this.GetClient(args?.TextView);
        if (client is null)
        {
            return false;
        }

        if (client.IsSessionActive)
        {
            return client.TryGoToNextField();
        }

        // Tab commits the completion list, and the list is what the author is looking at.
        if (this.completionBroker.IsCompletionActive(args!.TextView))
        {
            return false;
        }

        SnapshotPoint? caret = args.TextView.Caret.Position.Point.GetPoint(
            args.SubjectBuffer,
            PositionAffinity.Predecessor);
        if (caret is not { } point)
        {
            return false;
        }

        ITextSnapshotLine line = point.Snapshot.GetLineFromPosition(point.Position);
        int caretIndex = point.Position - line.Start.Position;
        string? shortcut = ViuSnippetShortcut.Find(
            ViuSnapshotLines.Read(point.Snapshot),
            line.LineNumber,
            caretIndex,
            out int start);
        return shortcut is not null &&
            client.TryExpand(shortcut, line.LineNumber, start, caretIndex);
    }

    /// <inheritdoc />
    public bool ExecuteCommand(ReturnKeyCommandArgs args, CommandExecutionContext context)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ViuSnippetExpansionClient? client = this.GetClient(args?.TextView);
        return client is { IsSessionActive: true } && client.TryComplete();
    }

    /// <inheritdoc />
    public bool ExecuteCommand(EscapeKeyCommandArgs args, CommandExecutionContext context)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ViuSnippetExpansionClient? client = this.GetClient(args?.TextView);
        return client is { IsSessionActive: true } && client.TryCancel();
    }

    private ViuSnippetExpansionClient? GetClient(ITextView? textView)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (textView is null)
        {
            return null;
        }

        if (textView.Properties.TryGetProperty(
                ClientPropertyKey,
                out ViuSnippetExpansionClient existing))
        {
            return existing;
        }

        ViuSnippetExpansionClient? created = this.CreateClient(textView);
        if (created is not null)
        {
            textView.Properties.AddProperty(ClientPropertyKey, created);
        }

        return created;
    }

    private ViuSnippetExpansionClient? CreateClient(ITextView textView)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        IVsTextView? viewAdapter = this.adapterFactory.GetViewAdapter(textView);
        if (viewAdapter is null ||
            this.serviceProvider.GetService(typeof(SVsTextManager)) is not IVsTextManager2 textManager ||
            textManager.GetExpansionManager(out IVsExpansionManager expansionManager) !=
                VSConstants.S_OK ||
            expansionManager is null ||
            viewAdapter.GetBuffer(out IVsTextLines textLines) != VSConstants.S_OK ||
            textLines is not IVsExpansion expansion)
        {
            return null;
        }

        return new ViuSnippetExpansionClient(viewAdapter, expansionManager, expansion);
    }
}
