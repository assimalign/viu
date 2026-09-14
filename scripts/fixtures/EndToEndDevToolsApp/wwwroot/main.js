import { dotnet } from './_framework/dotnet.js'

const panelMode = new URLSearchParams(window.location.search).has('panel')
let panel = null

// The inspected runtime's packaged bridge publishes to its own window. The embedding host only
// forwards complete server frames, preserving JSON as the sole application/client boundary.
const relay = (event) => {
    if (panelMode || event.source !== window || event.origin !== window.location.origin) return
    const frame = event.data
    if (frame?.channel !== 'assimalign.viu.devtools.server'
        && frame?.channel !== 'assimalign.viu.devtools.ready') return
    panel?.contentWindow?.postMessage(frame, window.location.origin)
}
window.addEventListener('message', relay)
window.addEventListener('pagehide', () => window.removeEventListener('message', relay), { once: true })

const { runMain, setModuleImports } = await dotnet.create()
setModuleImports('EndToEndDevToolsApp', {
    isPanel: () => panelMode,
    getOrigin: () => window.location.origin,
    getEndpoint: () => new URLSearchParams(window.location.search).get('endpoint') ?? ''
})

if (!panelMode) {
    panel = document.createElement('iframe')
    panel.title = 'Viu DevTools panel'
    panel.dataset.testid = 'devtools-frame'
    panel.src = '?panel'
    document.body.appendChild(panel)
}

await runMain()
