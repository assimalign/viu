import { dotnet } from './_framework/dotnet.js'

let exports
let nextHandle = 1

const nodes = new Map()
const nodeHandles = new WeakMap()
const listeners = new Map()

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
        createElement: tagName => registerNode(document.createElement(tagName)),
        createText: textContent => registerNode(document.createTextNode(textContent)),
        createComment: textContent => registerNode(document.createComment(textContent)),
        setText: (nodeHandle, textContent) => {
            getNode(nodeHandle).textContent = textContent
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
        appendChild: (parentHandle, childHandle) => {
            getNode(parentHandle).appendChild(getNode(childHandle))
        },
        insertBefore: (parentHandle, childHandle, beforeChildHandle) => {
            getNode(parentHandle).insertBefore(getNode(childHandle), getNode(beforeChildHandle))
        },
        removeChild: (parentHandle, childHandle) => {
            const parent = getNode(parentHandle)
            const child = getNode(childHandle)
            if (child.parentNode === parent) {
                parent.removeChild(child)
            }
        },
        clearChildren: nodeHandle => {
            getNode(nodeHandle).replaceChildren()
        },
        destroyNode: nodeHandle => {
            const node = nodes.get(nodeHandle)
            if (!node) {
                return
            }

            const nodeListeners = listeners.get(nodeHandle)
            if (nodeListeners) {
                for (const [eventName, listener] of nodeListeners.entries()) {
                    node.removeEventListener(eventName, listener)
                }
            }

            listeners.delete(nodeHandle)
            nodes.delete(nodeHandle)
            nodeHandles.delete(node)
        },
        setEventListener: (nodeHandle, eventName, callbackId) => {
            const node = getNode(nodeHandle)
            const nodeListeners = getListeners(nodeHandle)
            const existingListener = nodeListeners.get(eventName)
            if (existingListener) {
                node.removeEventListener(eventName, existingListener)
            }

            const listener = () => {
                exports.VuecsApp.DispatchEvent(callbackId)
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
