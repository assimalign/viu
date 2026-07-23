using Xunit;

// Reactivity intentionally uses ambient static state for the browser's single-threaded event-loop
// model, so tests must not execute concurrently.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
