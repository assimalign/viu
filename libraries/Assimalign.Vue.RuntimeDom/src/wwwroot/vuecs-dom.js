// The JS half of the Assimalign.Vue.RuntimeDom interop bridge — the browser implementation of
// Vue's nodeOps contract (@vue/runtime-dom nodeOps:
// https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/nodeOps.ts).
//
// Contract notes:
// - Handles are positive integers; 0 is the reserved "no node" sentinel.
// - Handle lifecycle is deterministic: removing a node releases the handles and DOM listeners
//   of every registered node in the removed subtree (no sweeps, no leaks). [V01.01.04.01]
// - Failures throw structured errors ("vuecs-dom|<operation>|<handle>|<message>") that the C#
//   side rethrows as typed BrowserDomException instances.
// - Keep every function a dumb leaf applier: decision logic (patchProp, diffing) lives on the
//   .NET side so ops stay expressible as opcodes for the command buffer ([V01.01.04.05]).

const namespaceUris = {
    svg: 'http://www.w3.org/2000/svg',
    mathml: 'http://www.w3.org/1998/Math/MathML'
}

const xlinkNamespaceUri = 'http://www.w3.org/1999/xlink'

let nextHandle = 1
const nodes = new Map()          // handle -> Node
const nodeHandles = new WeakMap() // Node -> handle
const listeners = new Map()      // handle -> Map(eventName -> listener)

let dispatchEvent = null         // .NET callback: (handle, eventName) => void

function fail(operation, handle, message) {
    throw new Error(`vuecs-dom|${operation}|${handle}|${message}`)
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

function getNode(operation, handle) {
    const node = nodes.get(handle)
    if (!node) {
        fail(operation, handle, 'unknown DOM handle')
    }

    return node
}

function releaseNodeHandle(node, released) {
    const handle = nodeHandles.get(node)
    if (handle === undefined) {
        return
    }

    const nodeListeners = listeners.get(handle)
    if (nodeListeners) {
        for (const [eventName, listener] of nodeListeners.entries()) {
            node.removeEventListener(eventName, listener)
        }
        listeners.delete(handle)
    }

    nodes.delete(handle)
    nodeHandles.delete(node)
    released.push(handle)
}

// Deterministic disposal: release the handle/listeners of every registered node inside the
// subtree rooted at `node`, including `node` itself. Returns the released handles so the
// .NET side can drop its listener-delegate entries for the same nodes in the same call.
function releaseSubtree(node, released) {
    releaseNodeHandle(node, released)
    if (node.nodeType !== Node.ELEMENT_NODE) {
        return released
    }

    const walker = document.createTreeWalker(node, NodeFilter.SHOW_ALL)
    let current = walker.nextNode()
    while (current) {
        releaseNodeHandle(current, released)
        current = walker.nextNode()
    }

    return released
}

export function initialize(dispatchCallback) {
    dispatchEvent = dispatchCallback
}

export const dom = {
    querySelector: selector => {
        const node = document.querySelector(selector)
        if (!node) {
            fail('querySelector', 0, `no DOM node matches selector '${selector}'`)
        }

        return registerNode(node)
    },

    createElement: (tagName, namespaceName) => {
        try {
            const namespaceUri = namespaceName ? namespaceUris[namespaceName] : undefined
            const element = namespaceUri
                ? document.createElementNS(namespaceUri, tagName)
                : document.createElement(tagName)
            return registerNode(element)
        } catch (error) {
            fail('createElement', 0, `cannot create <${tagName}>: ${error.message}`)
        }
    },

    createText: textContent => registerNode(document.createTextNode(textContent)),

    createComment: textContent => registerNode(document.createComment(textContent)),

    setText: (nodeHandle, textContent) => {
        getNode('setText', nodeHandle).textContent = textContent
    },

    setElementText: (nodeHandle, textContent) => {
        const element = getNode('setElementText', nodeHandle)
        // Replacing content drops any registered child handles deterministically first.
        const released = []
        let child = element.firstChild
        while (child) {
            releaseSubtree(child, released)
            child = child.nextSibling
        }
        element.textContent = textContent
        return released
    },

    insert: (parentHandle, childHandle, anchorHandle) => {
        const parent = getNode('insert', parentHandle)
        const child = getNode('insert', childHandle)
        if (anchorHandle === 0) {
            parent.appendChild(child)
            return
        }

        const anchor = getNode('insert', anchorHandle)
        if (anchor.parentNode !== parent) {
            fail('insert', anchorHandle, 'anchor is not a child of the target parent')
        }

        parent.insertBefore(child, anchor)
    },

    remove: childHandle => {
        const child = getNode('remove', childHandle)
        if (child.parentNode) {
            child.parentNode.removeChild(child)
        }

        return releaseSubtree(child, [])
    },

    parentNode: nodeHandle => {
        const parent = getNode('parentNode', nodeHandle).parentNode
        return parent ? registerNode(parent) : 0
    },

    nextSibling: nodeHandle => {
        const sibling = getNode('nextSibling', nodeHandle).nextSibling
        return sibling ? registerNode(sibling) : 0
    },

    // Inserts a multi-node static HTML chunk in ONE interop call via a detached <template>,
    // returning [firstHandle, lastHandle] as anchors for later patches (upstream:
    // insertStaticContent).
    insertStaticContent: (content, parentHandle, anchorHandle, namespaceName) => {
        const parent = getNode('insertStaticContent', parentHandle)
        const anchor = anchorHandle === 0 ? null : getNode('insertStaticContent', anchorHandle)
        const template = document.createElement('template')
        if (namespaceName === 'svg' || namespaceName === 'mathml') {
            // Parse inside the foreign-content root, then unwrap (upstream approach).
            template.innerHTML = namespaceName === 'svg' ? `<svg>${content}</svg>` : `<math>${content}</math>`
            const wrapper = template.content.firstChild
            const fragment = document.createDocumentFragment()
            while (wrapper.firstChild) {
                fragment.appendChild(wrapper.firstChild)
            }
            template.content.replaceChildren(fragment)
        } else {
            template.innerHTML = content
        }

        const first = template.content.firstChild
        const last = template.content.lastChild
        if (!first) {
            fail('insertStaticContent', parentHandle, 'static content produced no nodes')
        }

        parent.insertBefore(template.content, anchor)
        return [registerNode(first), registerNode(last)]
    },

    // --- element property/attribute leaves (driven by the .NET patchProp engine) -----------

    setAttribute: (nodeHandle, name, value) => {
        try {
            getNode('setAttribute', nodeHandle).setAttribute(name, value)
        } catch (error) {
            fail('setAttribute', nodeHandle, `cannot set '${name}': ${error.message}`)
        }
    },

    removeAttribute: (nodeHandle, name) => {
        getNode('removeAttribute', nodeHandle).removeAttribute(name)
    },

    setXlinkAttribute: (nodeHandle, name, value) => {
        getNode('setXlinkAttribute', nodeHandle).setAttributeNS(xlinkNamespaceUri, name, value)
    },

    removeXlinkAttribute: (nodeHandle, name) => {
        getNode('removeXlinkAttribute', nodeHandle)
            .removeAttributeNS(xlinkNamespaceUri, name.slice('xlink:'.length))
    },

    setClassName: (nodeHandle, value) => {
        getNode('setClassName', nodeHandle).className = value
    },

    setStringProperty: (nodeHandle, name, value) => {
        getNode('setStringProperty', nodeHandle)[name] = value
    },

    setBooleanProperty: (nodeHandle, name, value) => {
        getNode('setBooleanProperty', nodeHandle)[name] = value
    },

    // Compare-and-set in one interop call so an unchanged value never touches the DOM —
    // protects caret position and in-progress IME composition (upstream value handling).
    setValueGuarded: (nodeHandle, value) => {
        const element = getNode('setValueGuarded', nodeHandle)
        if (element.value !== value) {
            element.value = value
        }
    },

    setStyleText: (nodeHandle, cssText) => {
        getNode('setStyleText', nodeHandle).style.cssText = cssText
    },

    setStyleProperty: (nodeHandle, name, value, important) => {
        getNode('setStyleProperty', nodeHandle).style
            .setProperty(name, value, important ? 'important' : '')
    },

    removeStyleProperty: (nodeHandle, name) => {
        getNode('removeStyleProperty', nodeHandle).style.removeProperty(name)
    },

    // --- events (minimal set/remove; the invoker pattern and modifiers land with
    // [V01.01.04.03]) ------------------------------------------------------------------------

    addEventListener: (nodeHandle, eventName) => {
        const node = getNode('addEventListener', nodeHandle)
        let nodeListeners = listeners.get(nodeHandle)
        if (!nodeListeners) {
            nodeListeners = new Map()
            listeners.set(nodeHandle, nodeListeners)
        }

        if (nodeListeners.has(eventName)) {
            return
        }

        const listener = () => {
            if (dispatchEvent) {
                dispatchEvent(nodeHandle, eventName)
            }
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
    },

    // --- diagnostics ------------------------------------------------------------------------

    // Registry sizes for leak assertions (the [V01.01.04.01] stress criterion): [nodes, listener maps].
    getRegistrySizes: () => [nodes.size, listeners.size]
}
