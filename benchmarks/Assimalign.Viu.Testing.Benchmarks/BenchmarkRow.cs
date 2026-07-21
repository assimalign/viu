namespace Assimalign.Viu.Testing.Benchmarks;

/// <summary>
/// A synthetic table row — the unit the js-framework-benchmark scenarios operate on
/// (https://github.com/krausest/js-framework-benchmark). Carries a stable <see cref="Identifier"/>
/// (the keyed-diff key, globally unique across a run so a create-after-clear never reuses a key) and a
/// display <see cref="Label"/>. A readonly value type so a per-frame row list is cheap to snapshot.
/// </summary>
/// <param name="Identifier">The stable row id used as the keyed-diff key.</param>
/// <param name="Label">The row's display text.</param>
public readonly record struct BenchmarkRow(int Identifier, string Label);
