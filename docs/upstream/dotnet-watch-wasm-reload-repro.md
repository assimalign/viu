# `dotnet watch` does not reload a WebAssembly Browser App after a restart

Ready-to-paste upstream issue reproduction for .NET SDK 10.0.302 and the 10.0.11 browser-wasm
runtime pack on Windows.

## Suggested issue title

`dotnet watch` does not recognize WasmAppHost readiness after a rude-edit restart

## Reproduction

Create the stock WebAssembly Browser App:

```powershell
dotnet new wasmbrowser --name WatchWasmReloadRepro
cd WatchWasmReloadRepro
```

Replace `Properties/launchSettings.json` with:

```json
{
  "$schema": "http://json.schemastore.org/launchsettings.json",
  "profiles": {
    "WatchWasmReloadRepro": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "http://127.0.0.1:51235"
    }
  }
}
```

In `Program.cs`, add a label to the stock `StopwatchSample` and include it in the existing render
output. Keep the rest of the generated file unchanged:

```diff
 partial class StopwatchSample
 {
+    private static readonly string ReloadLabel = "before";
     private static Stopwatch stopwatch = new();

     public static void Start() => stopwatch.Start();
-    public static void Render() => SetInnerText("#time", stopwatch.Elapsed.ToString(@"mm\:ss"));
+    public static void Render() => SetInnerText(
+        "#time",
+        ReloadLabel + ": " + stopwatch.Elapsed.ToString(@"mm\:ss"));
 
     [JSImport("dom.setInnerText", "main.js")]
     internal static partial void SetInnerText(string selector, string content);
 }
```

Start the application. Do not set `DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER`:

```powershell
dotnet watch --non-interactive
```

After the page displays `before` and the browser-refresh client reports that it connected, change
only the field initializer:

```diff
-    private static readonly string ReloadLabel = "before";
+    private static readonly string ReloadLabel = "after";
```

## Expected result

`dotnet watch` rebuilds and restarts the application on port 51235, detects that the restarted
server is ready, sends `Reload` to the connected browser, and the page displays `after`.

## Actual result

The application rebuilds and restarts on port 51235, but the connected browser is not reloaded. A
representative sequence is:

```text
dotnet watch : Restart is needed to apply the changes.
... warning ENC0118: Changing 'field' might not have any effect until the application is restarted.
App url: http://127.0.0.1:51235/
```

The watch process reports the restart diagnostic, but the browser-refresh connection receives no
`Reload` message. The fixed `applicationUrl` proves this is not port drift.

## Source mismatch

In SDK 10.0.302,
[`WebServerProcessStateObserver`](https://github.com/dotnet/sdk/blob/v10.0.302/src/Dotnet.Watch/Watch/Process/WebServerProcessStateObserver.cs#L13-L42)
recognizes `Now listening on:` (plus the separate Aspire dashboard marker) and invokes the browser
callback only after matching that output. In runtime 10.0.11,
[`BrowserHost`](https://github.com/dotnet/runtime/blob/v10.0.11/src/mono/wasm/host/BrowserHost.cs#L71-L81)
prints `App url:` after WasmAppHost binds. There is no supported launch-profile or environment
setting that maps one readiness marker to the other.

Either accepting `App url:` in the watch observer or emitting the standard readiness marker from
WasmAppHost would restore the existing restart-reload path.
