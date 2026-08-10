using Xunit;

// Scheduler-backed scroll timing uses process-local execution state and must remain deterministic.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
