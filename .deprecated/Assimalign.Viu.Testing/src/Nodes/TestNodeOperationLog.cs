using System.Collections.Generic;

namespace Assimalign.Viu.Testing;

/// <summary>
/// The queryable, resettable record of every node operation the test adapter performed —
/// parity with the op recording in <c>@vue/runtime-test</c>. Op counts are the CoreCLR-side
/// proxy for JSImport call counts in the browser: a patch that logs one
/// <see cref="TestNodeOperationType.SetElementText"/> and nothing structural would cost exactly
/// one interop call on WASM.
/// </summary>
public sealed class TestNodeOperationLog
{
    private readonly List<TestNodeOperation> _operations = [];

    /// <summary>The recorded operations, oldest first.</summary>
    public IReadOnlyList<TestNodeOperation> Operations => _operations;

    /// <summary>Clears the log — typically right after mounting, to isolate a patch.</summary>
    public void Reset() => _operations.Clear();

    /// <summary>Counts the recorded operations of <paramref name="type"/>.</summary>
    /// <param name="type">The operation kind to count.</param>
    public int Count(TestNodeOperationType type)
    {
        var count = 0;
        foreach (var operation in _operations)
        {
            if (operation.Type == type)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>Returns the recorded operations of <paramref name="type"/>, oldest first.</summary>
    /// <param name="type">The operation kind to filter by.</param>
    public IReadOnlyList<TestNodeOperation> OfType(TestNodeOperationType type)
    {
        var matches = new List<TestNodeOperation>();
        foreach (var operation in _operations)
        {
            if (operation.Type == type)
            {
                matches.Add(operation);
            }
        }
        return matches;
    }

    /// <summary>
    /// The number of structural operations recorded (<see cref="TestNodeOperationType.Insert"/>
    /// plus <see cref="TestNodeOperationType.Remove"/>).
    /// </summary>
    public int StructuralOperationCount
        => Count(TestNodeOperationType.Insert) + Count(TestNodeOperationType.Remove);

    internal void Add(in TestNodeOperation operation) => _operations.Add(operation);
}
