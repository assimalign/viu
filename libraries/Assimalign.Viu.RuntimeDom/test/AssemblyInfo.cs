using Xunit;

// Consistent with the sibling suites: the runtime models a single-threaded JS event loop, so
// tests must not run in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
