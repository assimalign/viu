using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Assimalign.Viu.Tooling.Css;

namespace Assimalign.Viu.Sdk.Tasks;

/// <summary>
/// The MSBuild task that writes a consuming project's compiled <c>.viu</c> or compatible <c>.vue</c>
/// style CSS into a single deterministic bundle file, resolving the RS1035 limitation that a Roslyn source
/// generator emits C# and cannot perform <c>System.IO</c> ([V01.01.12.12]/[V01.01.06.09]). It runs
/// <b>outside</b> the analyzer sandbox (I/O
/// permitted) and re-runs the <em>same</em> shared <see cref="SingleFileComponentStyleBundler"/> over the
/// <em>same</em> component inputs the generator sees as <c>AdditionalFiles</c>, so the bytes it writes are
/// identical to the generator's <c>ExtractedStyles</c> constants — there is no second, divergent generation
/// path (see <c>docs/UTILITY-CSS-DESIGN.md</c> §2.4). The paired targets register the bundle as a
/// <c>StaticWebAsset</c> so <c>dotnet publish</c> ships it.
/// <para>
/// The task itself is incremental at two levels: the target that invokes it is gated by MSBuild
/// <c>Inputs</c>/<c>Outputs</c>, and the task additionally compares the freshly composed bundle against the
/// existing file and <b>skips the write</b> when the content is unchanged, so a no-op rebuild neither runs
/// the compile nor touches the file's timestamp. It reports whether it wrote through
/// <see cref="BundleWritten"/> and whether a bundle remains available through <see cref="BundleExists"/>.
/// When the final <c>@style</c> block is removed, the task deletes the previous output so a stale stylesheet
/// cannot remain registered as a static web asset. Compilation is recoverable — malformed CSS surfaces as a
/// generator diagnostic, not here — so the task never fails the build for authoring errors; only genuine I/O
/// failures are logged as errors.
/// </para>
/// </summary>
/// <remarks>Single-threaded, invoked once per build; not designed for concurrent use.</remarks>
public sealed class ViuBundleCss : Microsoft.Build.Utilities.Task, ICancelableTask
{
    private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();

    /// <summary>
    /// The consuming project's <c>.viu</c> and compatible <c>.vue</c> components — the same set flowed to the generator as
    /// <c>AdditionalFiles</c> (<c>@(ViuSingleFileComponent)</c>). Each item's <c>FullPath</c> is read and
    /// compiled; a component with no style block contributes nothing. The set may be empty for the
    /// paired target's cleanup-only pass after the final component file is removed.
    /// </summary>
    public ITaskItem[] SingleFileComponents { get; set; } = Array.Empty<ITaskItem>();

    /// <summary>
    /// The consuming project's directory (<c>$(ProjectDir)</c>). Used to derive each component's
    /// project-relative path — the input to the <c>data-v-&lt;hash&gt;</c> scope id and the bundle's
    /// deterministic ordering — so the task's scope ids match the generator's exactly.
    /// </summary>
    public string? ProjectDirectory { get; set; }

    /// <summary>
    /// The absolute path of the bundle file to write, under the intermediate output directory
    /// (e.g. <c>obj/&lt;config&gt;/&lt;tfm&gt;/viu/&lt;name&gt;.viu.css</c>). The directory is created if
    /// it does not exist.
    /// </summary>
    [Required]
    public string BundleOutputPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether removing the final style should replace an existing bundle with an empty
    /// stylesheet instead of deleting it. The development watch worker uses this so the browser's static
    /// asset hot-reload transport observes one final update; normal build and publish executions leave this
    /// disabled.
    /// </summary>
    public bool PreserveEmptyBundleOnRemoval { get; set; }

    /// <summary>
    /// Gets or sets the marker path that identifies a watch-only empty bundle. A subsequent normal build
    /// removes both the marker and tombstone before applying ordinary build semantics.
    /// </summary>
    public string EmptyBundleMarkerPath { get; set; } = string.Empty;

    /// <summary>
    /// The bundle path that now exists on disk, or the empty string when no component declared any
    /// <c>@style</c> block (in which case nothing is written and no static web asset should be registered).
    /// </summary>
    [Output]
    public string BundlePath { get; set; } = string.Empty;

    /// <summary>
    /// <see langword="true"/> when the task wrote (or rewrote) the bundle this run; <see langword="false"/>
    /// when the content was unchanged, or when no CSS remained (including a run that deleted an obsolete
    /// bundle). This output reports writes only; use <see cref="BundleExists"/> for the resulting availability
    /// state.
    /// </summary>
    [Output]
    public bool BundleWritten { get; set; }

    /// <summary>
    /// <see langword="true"/> when <see cref="BundleOutputPath"/> exists after the task completes;
    /// <see langword="false"/> when no component declared an <c>@style</c> block and any obsolete bundle was
    /// removed. The paired MSBuild targets use this state to avoid registering a stale static web asset.
    /// </summary>
    [Output]
    public bool BundleExists { get; set; }

    /// <inheritdoc />
    public void Cancel() => _cancellation.Cancel();

    /// <inheritdoc />
    public override bool Execute()
    {
        BundleExists = File.Exists(BundleOutputPath);
        BundlePath = BundleExists ? BundleOutputPath : string.Empty;
        BundleWritten = false;

        var inputs = new List<SingleFileComponentStyleInput>(SingleFileComponents.Length);
        foreach (var item in SingleFileComponents)
        {
            if (item is null)
            {
                continue;
            }

            var fullPath = item.GetMetadata("FullPath");
            if (string.IsNullOrEmpty(fullPath))
            {
                continue;
            }

            string text;
            try
            {
                text = File.ReadAllText(fullPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Log.LogErrorFromException(exception, showStackTrace: false);
                return false;
            }

            inputs.Add(new SingleFileComponentStyleInput(fullPath, text));
        }

        string? bundle;
        try
        {
            bundle = SingleFileComponentStyleBundler.Bundle(inputs, ProjectDirectory, _cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            // A cancelled build is not a failure; preserve the output state observed before compilation and
            // let MSBuild tear down.
            return !Log.HasLoggedErrors;
        }

        if (bundle is null)
        {
            return HandleEmptyBundle();
        }

        try
        {
            DeleteEmptyBundleMarker();

            // Incremental: only touch the file when the composed content actually differs, so a no-op
            // rebuild leaves the timestamp — and every downstream static-web-asset step — untouched.
            if (File.Exists(BundleOutputPath) &&
                string.Equals(File.ReadAllText(BundleOutputPath), bundle, StringComparison.Ordinal))
            {
                BundlePath = BundleOutputPath;
                BundleExists = true;
                BundleWritten = false;
                Log.LogMessage(MessageImportance.Low, "ViuBundleCss: bundle unchanged; skipped write of {0}.", BundleOutputPath);
                return true;
            }

            var directory = Path.GetDirectoryName(BundleOutputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // UTF-8 without a BOM, LF preserved — File.WriteAllText writes the string bytes verbatim, so the
            // LF-only bundle stays LF on every OS.
            File.WriteAllText(BundleOutputPath, bundle, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            BundlePath = BundleOutputPath;
            BundleExists = true;
            BundleWritten = true;
            Log.LogMessage(MessageImportance.Normal, "ViuBundleCss: wrote component style bundle to {0}.", BundleOutputPath);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.LogErrorFromException(exception, showStackTrace: false);
            return false;
        }
    }

    private bool HandleEmptyBundle()
    {
        try
        {
            if (PreserveEmptyBundleOnRemoval && File.Exists(BundleOutputPath))
            {
                if (new FileInfo(BundleOutputPath).Length != 0)
                {
                    File.WriteAllText(
                        BundleOutputPath,
                        string.Empty,
                        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    BundleWritten = true;
                    Log.LogMessage(
                        MessageImportance.Normal,
                        "ViuBundleCss: replaced the obsolete component style bundle with a hot-reload tombstone at {0}.",
                        BundleOutputPath);
                }

                WriteEmptyBundleMarker();
                BundlePath = BundleOutputPath;
                BundleExists = true;
                return true;
            }

            if (File.Exists(BundleOutputPath))
            {
                File.Delete(BundleOutputPath);
                Log.LogMessage(
                    MessageImportance.Normal,
                    "ViuBundleCss: deleted obsolete component style bundle at {0}.",
                    BundleOutputPath);
            }
            else
            {
                Log.LogMessage(
                    MessageImportance.Low,
                    "ViuBundleCss: no @style blocks found; no bundle written.");
            }

            DeleteEmptyBundleMarker();
            BundlePath = string.Empty;
            BundleExists = false;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.LogErrorFromException(exception, showStackTrace: false);
            return false;
        }
    }

    private void WriteEmptyBundleMarker()
    {
        if (string.IsNullOrEmpty(EmptyBundleMarkerPath) ||
            File.Exists(EmptyBundleMarkerPath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(EmptyBundleMarkerPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(
            EmptyBundleMarkerPath,
            string.Empty,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void DeleteEmptyBundleMarker()
    {
        if (!string.IsNullOrEmpty(EmptyBundleMarkerPath) &&
            File.Exists(EmptyBundleMarkerPath))
        {
            File.Delete(EmptyBundleMarkerPath);
        }
    }
}
