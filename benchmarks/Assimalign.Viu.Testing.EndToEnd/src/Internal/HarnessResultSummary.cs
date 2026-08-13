using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Testing.EndToEnd;

internal sealed record HarnessResultSummary(
    int SchemaVersion,
    DateTimeOffset GeneratedUtc,
    IReadOnlyList<ScenarioResult> Scenarios);
