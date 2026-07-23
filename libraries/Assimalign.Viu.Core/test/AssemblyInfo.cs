using Xunit;

// Core's scheduler and Reactivity use ambient state for the browser's single-threaded event-loop
// model, so their tests must not execute concurrently.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
