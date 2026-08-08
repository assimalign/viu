using Xunit;

// Reactivity's ambient graph and effect scopes target the single-threaded host event loop. Running
// these state lifetime tests concurrently would combine independent simulated host turns.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
