import { dotnet } from './_framework/dotnet.js'
import { customElementsBridge } from './_content/Assimalign.Viu.Browser/viu-dom.js'

// The host is ordinary HTML. These nodes exist before the runtime defines their element classes.
const counter = document.getElementById('counter')
const defaultChild = document.getElementById('default-child')
const lightCounter = document.getElementById('light-counter')
const elements = document.getElementById('elements')
const output = (name, value) => { document.getElementById(name).textContent = value }

document.body.addEventListener('changed', event => {
    output('event-result', JSON.stringify({
        detail: event.detail,
        bubbles: event.bubbles,
        composed: event.composed,
        target: event.target.id
    }))
})

function changeAttributes() {
    counter.setAttribute('count', '7')
    counter.setAttribute('count', '9')
    counter.setAttribute('enabled', 'false')
    counter.setAttribute('step-size', '2.5')
    counter.setAttribute('label-text', 'attributes & text')
    output('attribute-result', 'attribute batch submitted')
}

function changeProperties() {
    counter.count = 12
    counter.enabled = true
    counter.stepSize = 0.25
    counter.labelText = 'properties & text'
    output('property-result', 'property batch submitted')
}

function disconnect() {
    counter.remove()
    lightCounter.remove()
    output('lifecycle-result', 'disconnected')
}

function reconnect() {
    elements.append(counter, lightCounter)
    output('lifecycle-result', 'reconnected')
}

document.getElementById('change-attributes').addEventListener('click', changeAttributes)
document.getElementById('change-properties').addEventListener('click', changeProperties)
document.getElementById('disconnect').addEventListener('click', disconnect)
document.getElementById('reconnect').addEventListener('click', reconnect)

const { runMain, getAssemblyExports } = await dotnet.create()
await runMain()
const exports = await getAssemblyExports('EndToEndCustomElementApp.dll')
globalThis.customElementFixture = {
    managed: exports.EndToEndCustomElementApp.Program,
    bridge: customElementsBridge,
    counter,
    defaultChild,
    lightCounter,
    changeAttributes,
    changeProperties,
    disconnect,
    reconnect
}
output('status', 'ready')
