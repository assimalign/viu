// App bootstrap only: the DOM bridge ships with the Assimalign.Viu.RuntimeDom package
// (loaded by BrowserRuntime.InitializeAsync as /_content/Assimalign.Viu.RuntimeDom/viu-dom.js).
import { dotnet } from './_framework/dotnet.js'

const { runMain } = await dotnet.create()

await runMain()
