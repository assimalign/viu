// App bootstrap only: the DOM bridge now ships with the Assimalign.Vue.RuntimeDom package
// (loaded by BrowserRuntime.InitializeAsync as /_content/Assimalign.Vue.RuntimeDom/vuecs-dom.js).
import { dotnet } from './_framework/dotnet.js'

const { runMain } = await dotnet.create()

await runMain()
