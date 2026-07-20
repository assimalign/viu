using Xunit;

// The reactivity engine and the renderer's scheduler use ambient static state (single-threaded
// JS event-loop model), so the model and component tests must not run in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
