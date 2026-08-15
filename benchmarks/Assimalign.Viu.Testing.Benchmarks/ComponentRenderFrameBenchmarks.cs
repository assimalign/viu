using System;

using BenchmarkDotNet.Attributes;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Testing.Benchmarks;

/// <summary>
/// BenchmarkDotNet timings for the per-mount render-frame contract and cache paths changed by
/// [V01.01.06.14], #350. The fixed-size contract is the construction baseline; the provider-backed
/// contract measures the EnC-stable generated-code path specified by <c>[SFC-CG-9]</c>. First-fill
/// and repeated-hit controls keep any measured change attributable to contract resolution rather
/// than the compiler-owned cache behavior specified by <c>[SFC-OPT-1]</c>.
/// </summary>
[MemoryDiagnoser]
public class ComponentRenderFrameBenchmarks
{
    private const int CacheHitOperationCount = 1024;
    private const int RenderCacheSize = 4;
    private const int RenderCacheSlot = RenderCacheSize - 1;
    private static readonly Func<object> CacheFactory = static () => new object();
    private static readonly ComponentContract FixedContract = new(renderCacheSize: RenderCacheSize);
    private static readonly ComponentContract ProviderContract = new(
        displayName: null,
        renderCacheSizeProvider: ProvideRenderCacheSize);
    private ComponentRenderFrame _initializedFrame = null!;
    private object? _sink;

    /// <summary>Creates and initializes the reusable frame for the cache-hit control.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _initializedFrame = new ComponentRenderFrame(FixedContract);
        _initializedFrame.GetOrAddCache(RenderCacheSlot, CacheFactory);
    }

    /// <summary>Constructs a four-slot frame from a fixed-size component contract.</summary>
    /// <returns>The newly allocated mount-owned render frame.</returns>
    [Benchmark(Baseline = true)]
    public ComponentRenderFrame ConstructFrameFromFixedContract() => new(FixedContract);

    /// <summary>Constructs a four-slot frame after invoking the generated-code cache-size provider.</summary>
    /// <returns>The newly allocated mount-owned render frame.</returns>
    [Benchmark]
    public ComponentRenderFrame ConstructFrameFromProviderContract() => new(ProviderContract);

    /// <summary>Constructs a four-slot frame and initializes its highest cache slot.</summary>
    /// <returns>The initialized mount-owned render frame.</returns>
    [Benchmark]
    public ComponentRenderFrame ConstructFrameAndFillFirstCacheValue()
    {
        var frame = new ComponentRenderFrame(RenderCacheSize);
        _sink = frame.GetOrAddCache(RenderCacheSlot, CacheFactory);
        return frame;
    }

    /// <summary>
    /// Reads an initialized cache slot repeatedly, amortizing BenchmarkDotNet's empty-method overhead.
    /// </summary>
    /// <returns>The stable cached value from the last read.</returns>
    [Benchmark(OperationsPerInvoke = CacheHitOperationCount)]
    public object ReadInitializedCacheValue()
    {
        object value = null!;
        for (var index = 0; index < CacheHitOperationCount; index++)
        {
            value = _initializedFrame.GetOrAddCache(RenderCacheSlot, CacheFactory);
        }
        return value;
    }

    private static int ProvideRenderCacheSize() => RenderCacheSize;
}
