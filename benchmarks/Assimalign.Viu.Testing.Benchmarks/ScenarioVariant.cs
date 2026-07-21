namespace Assimalign.Viu.Testing.Benchmarks;

/// <summary>
/// How a row list is turned into a virtual-node tree — the two shapes whose interop-crossing costs the
/// suite contrasts.
/// </summary>
public enum ScenarioVariant
{
    /// <summary>
    /// The framework's real compiled output: rows are keyed (the keyed diff with the longest-increasing
    /// subsequence moves nodes instead of rebuilding them) and dynamic parts carry <c>PatchFlags</c>
    /// (a label change is one targeted set-text, a selection is one class patch). This is what the
    /// baseline records and the gate protects.
    /// </summary>
    Optimized,

    /// <summary>
    /// The deliberate optimization bypass: rows carry no key and no patch flag, so the renderer falls
    /// back to a positional unkeyed diff and a full per-element property/children walk. Structurally
    /// identical output, but a middle-of-list remove or reorder now re-patches every shifted row —
    /// the same class of regression a command-buffer bypass would cause in the browser (time-neutral,
    /// but many more boundary crossings). Used only to prove the interop-count gate is sensitive.
    /// </summary>
    KeylessBypass,
}
