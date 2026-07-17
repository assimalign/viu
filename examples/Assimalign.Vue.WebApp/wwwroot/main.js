import { dotnet } from './_framework/dotnet.js'

// Browser side of the Vuecs node-ops contract (RendererOptions<int> in
// Assimalign.Vue.RuntimeCore). Handles are positive integers; 0 is the reserved
// "no node" sentinel the renderer passes for absent anchors/parents.
// This is the example-level bridge — the production RuntimeDom bridge with
// deterministic handle disposal and typed error propagation lands with [V01.01.04.01].

let exports
let nextHandle = 1

const nodes = new Map()
const nodeHandles = new WeakMap()
const listeners = new Map()

const namespaceUris = {
    svg: 'http://www.w3.org/2000/svg',
    mathml: 'http://www.w3.org/1998/Math/MathML'
}

function registerNode(node) {
    const existingHandle = nodeHandles.get(node)
    if (existingHandle !== undefined) {
        return existingHandle
    }

    const handle = nextHandle++
    nodeHandles.set(node, handle)
    nodes.set(handle, node)
    return handle
}

function getNode(handle) {
    const node = nodes.get(handle)
    if (!node) {
        throw new Error(`Unknown DOM handle: ${handle}`)
    }

    return node
}

function getListeners(handle) {
    let nodeListeners = listeners.get(handle)
    if (!nodeListeners) {
        nodeListeners = new Map()
        listeners.set(handle, nodeListeners)
    }

    return nodeListeners
}

// Example-level handle hygiene: after a removal, sweep registry entries whose nodes
// left the document, releasing their handles and DOM listeners. [V01.01.04.01]
// replaces this with deterministic per-node disposal.
function sweepDisconnected() {
    for (const [handle, node] of nodes) {
        if (!node.isConnected) {
            const nodeListeners = listeners.get(handle)
            if (nodeListeners) {
                for (const [eventName, listener] of nodeListeners.entries()) {
                    node.removeEventListener(eventName, listener)
                }
                listeners.delete(handle)
            }

            nodes.delete(handle)
            nodeHandles.delete(node)
        }
    }
}

const { setModuleImports, getAssemblyExports, getConfig, runMain } = await dotnet.create()

setModuleImports('main.js', {
    dom: {
        querySelector: selector => {
            const node = document.querySelector(selector)
            if (!node) {
                throw new Error(`No DOM node matches selector '${selector}'.`)
            }

            return registerNode(node)
        },
        createElement: (tagName, namespaceName) => {
            const namespaceUri = namespaceName ? namespaceUris[namespaceName] : undefined
            const element = namespaceUri
                ? document.createElementNS(namespaceUri, tagName)
                : document.createElement(tagName)
            return registerNode(element)
        },
        createText: textContent => registerNode(document.createTextNode(textContent)),
        createComment: textContent => registerNode(document.createComment(textContent)),
        setText: (nodeHandle, textContent) => {
            getNode(nodeHandle).textContent = textContent
        },
        setElementText: (nodeHandle, textContent) => {
            getNode(nodeHandle).textContent = textContent
        },
        insert: (parentHandle, childHandle, anchorHandle) => {
            const parent = getNode(parentHandle)
            const child = getNode(childHandle)
            if (anchorHandle === 0) {
                parent.appendChild(child)
            } else {
                parent.insertBefore(child, getNode(anchorHandle))
            }
        },
        remove: childHandle => {
            const child = nodes.get(childHandle)
            if (child && child.parentNode) {
                child.parentNode.removeChild(child)
            }

            sweepDisconnected()
        },
        parentNode: nodeHandle => {
            const parent = getNode(nodeHandle).parentNode
            return parent ? registerNode(parent) : 0
        },
        nextSibling: nodeHandle => {
            const sibling = getNode(nodeHandle).nextSibling
            return sibling ? registerNode(sibling) : 0
        },
        setProperty: (nodeHandle, name, value) => {
            const node = getNode(nodeHandle)
            if (name === 'class') {
                node.className = value
                return
            }

            if (name === 'value') {
                node.value = value
            }

            node.setAttribute(name, value)
        },
        setBooleanProperty: (nodeHandle, name, value) => {
            const node = getNode(nodeHandle)
            node[name] = value

            if (value) {
                node.setAttribute(name, '')
            } else {
                node.removeAttribute(name)
            }
        },
        removeProperty: (nodeHandle, name) => {
            const node = getNode(nodeHandle)

            if (name === 'class') {
                node.className = ''
                node.removeAttribute('class')
                return
            }

            if (name === 'value') {
                node.value = ''
            }

            if (name in node && typeof node[name] === 'boolean') {
                node[name] = false
            }

            node.removeAttribute(name)
        },
        setEventListener: (nodeHandle, eventName) => {
            const node = getNode(nodeHandle)
            const nodeListeners = getListeners(nodeHandle)
            if (nodeListeners.has(eventName)) {
                return
            }

            const listener = () => {
                exports.VuecsApp.DispatchEvent(nodeHandle, eventName)
            }

            node.addEventListener(eventName, listener)
            nodeListeners.set(eventName, listener)
        },
        removeEventListener: (nodeHandle, eventName) => {
            const node = nodes.get(nodeHandle)
            if (!node) {
                return
            }

            const nodeListeners = listeners.get(nodeHandle)
            const listener = nodeListeners?.get(eventName)
            if (!listener) {
                return
            }

            node.removeEventListener(eventName, listener)
            nodeListeners.delete(eventName)
        }
    }
})

const config = getConfig()
exports = await getAssemblyExports(config.mainAssemblyName)

await runMain()
