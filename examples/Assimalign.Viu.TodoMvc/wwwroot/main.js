// App bootstrap only: the DOM bridge ships with the Assimalign.Viu.Browser package and is loaded
// by BrowserRuntime.InitializeAsync (/_content/Assimalign.Viu.Browser/viu-dom.js).
import { dotnet } from './_framework/dotnet.js'

const { runMain } = await dotnet.create()

await runMain()
