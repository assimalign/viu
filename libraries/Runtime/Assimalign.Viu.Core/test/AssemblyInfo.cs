using Xunit;

// Core's scheduler is intentionally ambient and single-threaded, so tests that exercise it must
// not overlap another renderer or scheduler test in this assembly.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
