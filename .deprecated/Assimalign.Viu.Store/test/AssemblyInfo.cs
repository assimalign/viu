using Xunit;

// The store system uses ambient static state (the active registry) and drives the reactivity engine's
// ambient scope/subscriber — the single-threaded JS event-loop model — so tests must not run in
// parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
