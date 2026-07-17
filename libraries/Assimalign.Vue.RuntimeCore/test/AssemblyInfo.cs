using Xunit;

// The scheduler and reactivity engine use ambient static state (single-threaded JS event-loop
// model), so tests must not run in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
