using Xunit;

// The scheduler and reactivity engine use ambient static state (single-threaded JS event-loop
// model), so tests must not run in parallel. This became load-bearing once [V01.01.06.07] added a
// second test class (SingleFileComponentMountTests) alongside CompiledRenderEndToEndTests: both drive
// the process-global Scheduler through ViuTest.Mount / TestSchedulerPump, so a parallel run of the two
// classes would reset the scheduler out from under an in-flight mount.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
