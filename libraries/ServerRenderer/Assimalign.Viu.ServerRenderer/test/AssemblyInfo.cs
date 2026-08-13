using Xunit;

// The server renderer drives Core's ambient component context and the reactivity engine's active
// subscriber/scope. Tests run serially to match the runtime's single-threaded event-loop model.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
