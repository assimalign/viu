namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// One auto-closing insertion: the text to place at the caret and where the caret lands inside it.
/// </summary>
/// <param name="InsertedText">
/// Everything the edit writes at the caret, the character the user typed included. The typed
/// character is part of the insertion rather than left to the editor so the whole completion is one
/// buffer change and therefore one undo unit.
/// </param>
/// <param name="CaretOffset">
/// The caret's position after the edit, as an offset into <paramref name="InsertedText"/>. Zero
/// leaves the caret before the insertion; <see cref="string.Length"/> leaves it after.
/// </param>
/// <remarks>
/// A value type with no editor dependency, so the decisions that produce it stay unit-testable
/// ([V01.01.12.07.08]).
/// </remarks>
internal readonly record struct ViuAutoClosingEdit(string InsertedText, int CaretOffset);
