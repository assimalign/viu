using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Testing.EndToEnd;

internal sealed record StartupMeasurementResult(
    int SchemaVersion,
    DateTimeOffset GeneratedUtc,
    string Sample,
    string Measurement,
    string BrowserEngine,
    int WarmupRuns,
    int MeasuredRuns,
    IReadOnlyList<double> StartupMilliseconds,
    double MedianStartupMilliseconds);
