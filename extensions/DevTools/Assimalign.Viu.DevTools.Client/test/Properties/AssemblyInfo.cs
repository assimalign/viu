using Xunit;

// [EXE-1]: component panes and their testing hosts share one application event loop.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
