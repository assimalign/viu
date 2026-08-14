using System;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.LanguageServer;

/// <summary>
/// Coordinates cancellation and ordered writes for one document's asynchronous publication rounds.
/// </summary>
internal sealed class LanguageServerDocumentPublicationState
{
    private readonly object synchronization = new();
    private long generation;
    private CancellationTokenSource? currentCancellation;
    private int activeFeatureRequests;
    private TaskCompletionSource? featureRequestsCompleted;
    private long nextProjectContextGeneration;
    private long appliedProjectContextGeneration;
    private long nextClassCatalogGeneration;
    private long appliedClassCatalogGeneration;

    /// <summary>Serializes the document's classification and diagnostic notification pair.</summary>
    internal SemaphoreSlim WriteGate { get; } = new(1, 1);

    /// <summary>Cancels the previous round and begins a new current generation.</summary>
    internal (long Generation, CancellationTokenSource Cancellation) Begin(
        CancellationToken hostCancellation)
    {
        var nextCancellation = CancellationTokenSource.CreateLinkedTokenSource(hostCancellation);
        CancellationTokenSource? previousCancellation;
        long nextGeneration;
        lock (synchronization)
        {
            previousCancellation = currentCancellation;
            currentCancellation = nextCancellation;
            nextGeneration = ++generation;
        }

        Cancel(previousCancellation);
        return (nextGeneration, nextCancellation);
    }

    /// <summary>Cancels any current round and invalidates its generation.</summary>
    internal bool CancelCurrent()
    {
        CancellationTokenSource? cancellation;
        lock (synchronization)
        {
            cancellation = currentCancellation;
            if (cancellation is null)
            {
                return false;
            }

            currentCancellation = null;
            generation++;
        }

        Cancel(cancellation);
        return true;
    }

    /// <summary>
    /// Invalidates the current publication and opens the latency-sensitive feature-request barrier.
    /// </summary>
    /// <returns><see langword="true"/> when a live publication needs replacement.</returns>
    internal bool BeginFeatureRequest()
    {
        CancellationTokenSource? cancellation;
        lock (synchronization)
        {
            activeFeatureRequests++;
            if (activeFeatureRequests == 1)
            {
                featureRequestsCompleted = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }

            cancellation = currentCancellation;
            if (cancellation is not null)
            {
                currentCancellation = null;
                generation++;
            }
        }

        Cancel(cancellation);
        return cancellation is not null;
    }

    /// <summary>Closes one feature-request lease and releases a waiting publication when last.</summary>
    internal void EndFeatureRequest()
    {
        TaskCompletionSource? completed = null;
        lock (synchronization)
        {
            activeFeatureRequests--;
            if (activeFeatureRequests == 0)
            {
                completed = featureRequestsCompleted;
                featureRequestsCompleted = null;
            }
        }

        completed?.TrySetResult();
    }

    /// <summary>Waits until every earlier semantic feature request has written its response.</summary>
    internal Task WaitForFeatureRequestsAsync(CancellationToken cancellationToken)
    {
        Task? wait;
        lock (synchronization)
        {
            wait = featureRequestsCompleted?.Task;
        }

        return wait is null
            ? Task.CompletedTask
            : wait.WaitAsync(cancellationToken);
    }

    /// <summary>Applies a short service mutation only while the candidate remains current.</summary>
    internal bool TryApplyCurrent(
        long candidateGeneration,
        CancellationTokenSource cancellation,
        Action apply)
    {
        ArgumentNullException.ThrowIfNull(apply);
        lock (synchronization)
        {
            if (generation != candidateGeneration ||
                !ReferenceEquals(currentCancellation, cancellation) ||
                cancellation.IsCancellationRequested)
            {
                return false;
            }

            apply();
            return true;
        }
    }

    /// <summary>Reserves independent ordering generations for the inputs this feature discovers.</summary>
    internal (long? ProjectContext, long? ClassCatalog) BeginFeatureConfiguration(
        bool includesProjectContext,
        bool includesClassCatalog)
    {
        lock (synchronization)
        {
            var projectContext = includesProjectContext
                ? ++nextProjectContextGeneration
                : (long?)null;
            var classCatalog = includesClassCatalog
                ? ++nextClassCatalogGeneration
                : (long?)null;
            return (projectContext, classCatalog);
        }
    }

    /// <summary>
    /// Applies each discovered input only while its document request remains live and no later
    /// discovery in the same input channel has already applied. Closing the document uses the same
    /// gate, so a mutation that wins the race is evicted by close and a mutation that loses the
    /// race observes cancellation before it can repopulate.
    /// </summary>
    internal bool TryApplyFeatureRequest(
        long? projectContextGeneration,
        Action? applyProjectContext,
        long? classCatalogGeneration,
        Action? applyClassCatalog,
        CancellationToken cancellationToken)
    {
        lock (synchronization)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            var applied = false;
            if (projectContextGeneration is long projectGeneration &&
                projectGeneration >= appliedProjectContextGeneration)
            {
                ArgumentNullException.ThrowIfNull(applyProjectContext);
                applyProjectContext();
                appliedProjectContextGeneration = projectGeneration;
                applied = true;
            }

            if (classCatalogGeneration is long catalogGeneration &&
                catalogGeneration >= appliedClassCatalogGeneration)
            {
                ArgumentNullException.ThrowIfNull(applyClassCatalog);
                applyClassCatalog();
                appliedClassCatalogGeneration = catalogGeneration;
                applied = true;
            }

            return applied;
        }
    }

    /// <summary>Serializes document-cache eviction with short feature-request mutations.</summary>
    internal void ApplyDocumentClose(Action apply)
    {
        ArgumentNullException.ThrowIfNull(apply);
        lock (synchronization)
        {
            apply();
        }
    }

    /// <summary>Clears a successfully finished source without invalidating a newer generation.</summary>
    internal void Complete(long candidateGeneration, CancellationTokenSource cancellation)
    {
        lock (synchronization)
        {
            if (generation == candidateGeneration &&
                ReferenceEquals(currentCancellation, cancellation))
            {
                currentCancellation = null;
            }
        }
    }

    /// <summary>Reports whether a round remains the newest document generation.</summary>
    internal bool IsCurrent(long candidateGeneration, CancellationTokenSource cancellation)
    {
        lock (synchronization)
        {
            return generation == candidateGeneration &&
                   ReferenceEquals(currentCancellation, cancellation) &&
                   !cancellation.IsCancellationRequested;
        }
    }

    private static void Cancel(CancellationTokenSource? cancellation)
    {
        if (cancellation is null)
        {
            return;
        }

        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The completed prior round disposed its source before the loop advanced the document.
        }
    }
}
