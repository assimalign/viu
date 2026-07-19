using Xunit;

// The scheduler and test tree use ambient state under the single-threaded JS event-loop model,
// so tests must not run in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
