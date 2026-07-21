// App bootstrap only: the DOM bridge now ships with the Assimalign.Viu.Browser package
// (loaded by BrowserRuntime.InitializeAsync as /_content/Assimalign.Viu.Browser/viu-dom.js).
import { dotnet } from './_framework/dotnet.js'

const { runMain } = await dotnet.create()

await runMain()
