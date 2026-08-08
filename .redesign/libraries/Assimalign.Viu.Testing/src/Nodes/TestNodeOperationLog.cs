using System.Collections.Generic;

namespace Assimalign.Viu.Testing;

/// <summary>Records and queries every host operation issued through the test renderer options.</summary>
/// <remarks>
/// Commit counts expose Core's per-renderer batch seam without a browser. Specified by
/// <c>[RND-HOST-4]</c>, <c>[RND-IO-1]</c>, and <c>[CONF-3]</c>.
/// </remarks>
public sealed class TestNodeOperationLog
{
    private readonly List<TestNodeOperation> _operations = [];

    /// <summary>Gets recorded operations in execution order.</summary>
    public IReadOnlyList<TestNodeOperation> Operations => _operations;

    /// <summary>Clears all recorded operations.</summary>
    public void Reset() => _operations.Clear();

    /// <summary>Counts operations of the requested kind.</summary>
    /// <param name="type">The operation kind.</param>
    /// <returns>The matching count.</returns>
    public int Count(TestNodeOperationType type)
    {
        int count = 0;
        for (int index = 0; index < _operations.Count; index++)
        {
            if (_operations[index].Type == type)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Returns operations of the requested kind in execution order.</summary>
    /// <param name="type">The operation kind.</param>
    /// <returns>A detached list of matching operations.</returns>
    public IReadOnlyList<TestNodeOperation> OfType(TestNodeOperationType type)
    {
        List<TestNodeOperation> matches = [];
        for (int index = 0; index < _operations.Count; index++)
        {
            TestNodeOperation operation = _operations[index];
            if (operation.Type == type)
            {
                matches.Add(operation);
            }
        }

        return matches;
    }

    /// <summary>Gets the total number of insert and remove operations.</summary>
    public int StructuralOperationCount =>
        Count(TestNodeOperationType.Insert) + Count(TestNodeOperationType.Remove);

    internal void Add(in TestNodeOperation operation) => _operations.Add(operation);
}
