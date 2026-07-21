using Xunit;

// The server renderer drives RuntimeCore's ambient single-threaded machinery — the active
// component-instance stack (ComponentInstance.Current), the block-tree accumulator, and the
// reactivity engine's active subscriber/scope. Renders must therefore not interleave on the same
// thread, so the suite runs serially (the single-threaded JS event-loop model the runtime targets).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
