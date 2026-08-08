using Xunit;

// The RouterView/RouterLink suites drive the runtime renderer, whose scheduler and reactivity engine
// use ambient static state (single-threaded JS event-loop model), so tests must not run in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
