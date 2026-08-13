namespace Assimalign.Viu.Testing.EndToEnd;

internal sealed record ScenarioResult(
    string BrowserEngine,
    string Scenario,
    bool Succeeded,
    double DurationMilliseconds,
    string? Failure,
    string? ScreenshotPath,
    string? TracePath);
