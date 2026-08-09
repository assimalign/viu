using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Assimalign.Viu.Testing.EndToEnd;

internal sealed class HarnessOptions
{
    private HarnessOptions()
    {
    }

    internal required string BrowserRootDirectory { get; init; }

    internal required string HydrationRootDirectory { get; init; }

    internal required string ArtifactDirectory { get; init; }

    internal IReadOnlyList<BrowserEngine> BrowserEngines { get; init; } =
        [BrowserEngine.Chromium];

    internal bool Headed { get; init; }

    internal bool MeasureStartup { get; init; }

    internal string? StartupResultsPath { get; init; }

    internal int StartupWarmupRuns { get; init; } = 1;

    internal int StartupMeasuredRuns { get; init; } = 10;

    internal static HarnessOptions Parse(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        string? browserRootDirectory = null;
        string? hydrationRootDirectory = null;
        string? artifactDirectory = null;
        string? startupResultsPath = null;
        List<BrowserEngine> browserEngines = [];
        bool headed = false;
        bool measureStartup = false;
        int startupWarmupRuns = 1;
        int startupMeasuredRuns = 10;

        for (int index = 0; index < arguments.Length; index++)
        {
            string argument = arguments[index];
            switch (argument)
            {
                case "--browser-root":
                    browserRootDirectory = ReadValue(arguments, ref index, argument);
                    break;
                case "--hydration-root":
                    hydrationRootDirectory = ReadValue(arguments, ref index, argument);
                    break;
                case "--artifacts":
                    artifactDirectory = ReadValue(arguments, ref index, argument);
                    break;
                case "--browser-engine":
                    AddBrowserEngine(
                        browserEngines,
                        ReadValue(arguments, ref index, argument));
                    break;
                case "--headed":
                    headed = true;
                    break;
                case "--measure-startup":
                    measureStartup = true;
                    break;
                case "--startup-results":
                    startupResultsPath = ReadValue(arguments, ref index, argument);
                    break;
                case "--startup-warmup-runs":
                    startupWarmupRuns = ReadPositiveInteger(
                        ReadValue(arguments, ref index, argument),
                        argument);
                    break;
                case "--startup-measured-runs":
                    startupMeasuredRuns = ReadPositiveInteger(
                        ReadValue(arguments, ref index, argument),
                        argument);
                    break;
                default:
                    throw new ArgumentException(
                        $"Unknown end-to-end harness argument: {argument}");
            }
        }

        ArgumentException.ThrowIfNullOrEmpty(browserRootDirectory);
        ArgumentException.ThrowIfNullOrEmpty(hydrationRootDirectory);
        ArgumentException.ThrowIfNullOrEmpty(artifactDirectory);
        if (!Directory.Exists(browserRootDirectory))
        {
            throw new DirectoryNotFoundException(
                $"The Browser fixture publish root does not exist: {browserRootDirectory}");
        }

        if (!Directory.Exists(hydrationRootDirectory))
        {
            throw new DirectoryNotFoundException(
                $"The hydration fixture publish root does not exist: {hydrationRootDirectory}");
        }

        if (browserEngines.Count == 0)
        {
            browserEngines.Add(BrowserEngine.Chromium);
        }

        if (measureStartup)
        {
            ArgumentException.ThrowIfNullOrEmpty(startupResultsPath);
            if (startupMeasuredRuns < 10)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startupMeasuredRuns),
                    startupMeasuredRuns,
                    "Startup measurement requires at least ten post-warm-up runs.");
            }

            if (!browserEngines.Contains(BrowserEngine.Chromium))
            {
                throw new ArgumentException(
                    "Startup measurement requires Chromium in the selected browser engines.");
            }
        }

        return new HarnessOptions
        {
            BrowserRootDirectory = Path.GetFullPath(browserRootDirectory),
            HydrationRootDirectory = Path.GetFullPath(hydrationRootDirectory),
            ArtifactDirectory = Path.GetFullPath(artifactDirectory),
            BrowserEngines = browserEngines.AsReadOnly(),
            Headed = headed,
            MeasureStartup = measureStartup,
            StartupResultsPath = startupResultsPath is null
                ? null
                : Path.GetFullPath(startupResultsPath),
            StartupWarmupRuns = startupWarmupRuns,
            StartupMeasuredRuns = startupMeasuredRuns,
        };
    }

    private static string ReadValue(
        IReadOnlyList<string> arguments,
        ref int index,
        string option)
    {
        index++;
        if (index >= arguments.Count || string.IsNullOrWhiteSpace(arguments[index]))
        {
            throw new ArgumentException($"{option} requires a non-empty value.");
        }

        return arguments[index];
    }

    private static int ReadPositiveInteger(string value, string option)
    {
        if (!int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int result)
            || result < 1)
        {
            throw new ArgumentOutOfRangeException(
                option,
                value,
                $"{option} must be a positive integer.");
        }

        return result;
    }

    private static void AddBrowserEngine(
        ICollection<BrowserEngine> browserEngines,
        string value)
    {
        if (string.Equals(value, "all", StringComparison.OrdinalIgnoreCase))
        {
            AddDistinct(browserEngines, BrowserEngine.Chromium);
            AddDistinct(browserEngines, BrowserEngine.Firefox);
            AddDistinct(browserEngines, BrowserEngine.WebKit);
            return;
        }

        if (!Enum.TryParse(value, ignoreCase: true, out BrowserEngine engine))
        {
            throw new ArgumentException(
                $"Unknown browser engine '{value}'. Use Chromium, Firefox, WebKit, or All.");
        }

        AddDistinct(browserEngines, engine);
    }

    private static void AddDistinct(
        ICollection<BrowserEngine> browserEngines,
        BrowserEngine browserEngine)
    {
        if (!browserEngines.Contains(browserEngine))
        {
            browserEngines.Add(browserEngine);
        }
    }
}
