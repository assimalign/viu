using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser;

internal sealed class CustomElementInstance
{
    internal required int Identifier { get; init; }
    internal required int Container { get; init; }
    internal required CustomElementDefinition Definition { get; init; }
    internal required ApplicationContext Application { get; init; }
    internal required SchedulerJob Job { get; init; }
    internal ComponentContext? Context { get; set; }
    internal Dictionary<string, object?> Parameters { get; } = new(StringComparer.Ordinal);
    internal Dictionary<string, ComponentSlot> Slots { get; } = new(StringComparer.Ordinal);
    internal HashSet<JSObject> Objects { get; } = [];
}
