namespace Assimalign.Viu.Browser;

/// <summary>The resolved CSS class names shared by transition built-ins.</summary>
internal readonly record struct DomTransitionClassNames(
    string EnterFrom,
    string EnterActive,
    string EnterTo,
    string AppearFrom,
    string AppearActive,
    string AppearTo,
    string LeaveFrom,
    string LeaveActive,
    string LeaveTo);
