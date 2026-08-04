using Xunit;

// The scheduler and test tree use ambient state under the single-threaded JS event-loop model
// ([EXE-1]), so tests must not run in parallel — mounting two compiled components at once would have
// them share one scheduler.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
