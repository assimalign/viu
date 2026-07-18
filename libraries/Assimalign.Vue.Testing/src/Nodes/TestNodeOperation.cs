namespace Assimalign.Vue.Testing;

/// <summary>
/// One recorded node operation: the operation type, its target, and its arguments — parity with
/// the op records in <c>@vue/runtime-test</c>. A struct so logging stays allocation-light and
/// does not distort the patch-efficiency numbers it exists to measure.
/// </summary>
/// <param name="Type">The operation kind.</param>
/// <param name="TargetNode">The node the operation created or acted on.</param>
/// <param name="ParentNode">The parent for insert/remove operations, when known.</param>
/// <param name="AnchorNode">The insert-before anchor, or null when appending.</param>
/// <param name="PropertyName">The prop name for <see cref="TestNodeOperationType.PatchProperty"/>.</param>
/// <param name="PreviousValue">The prior prop value for <see cref="TestNodeOperationType.PatchProperty"/>.</param>
/// <param name="NextValue">The new prop value for <see cref="TestNodeOperationType.PatchProperty"/>.</param>
/// <param name="Text">The text payload for create/set-text/static operations.</param>
public readonly record struct TestNodeOperation(
    TestNodeOperationType Type,
    TestNode? TargetNode = null,
    TestNode? ParentNode = null,
    TestNode? AnchorNode = null,
    string? PropertyName = null,
    object? PreviousValue = null,
    object? NextValue = null,
    string? Text = null);
