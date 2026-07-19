// Benchmark + diagnostics support for the ?diagnostics=1 mode of the example app.
// Provides the JSObject-marshaling strategy counterparts of the int-handle bridge ops, so the
// [V01.01.04.01] ADR can measure both strategies over identical op mixes, plus small helpers.
// This module is example-only tooling; it ships with the demo, not with the framework.

export const benchmark = {
    getQuery: () => location.search,

    now: () => performance.now(),

    // Surfaces diagnostics-mode failures in the DOM (console capture is not always available
    // in embedded panes).
    reportCrash: text => {
        document.title = 'VIU DIAGNOSTICS ERROR'
        const pre = document.createElement('pre')
        pre.style.cssText = 'text-align:left;white-space:pre-wrap;color:#b00;font-size:12px'
        pre.textContent = text
        document.body.appendChild(pre)
    },

    // --- JSObject strategy: elements cross the boundary as proxied object references --------

    createElementObject: tagName => document.createElement(tagName),

    setElementTextObject: (element, text) => {
        element.textContent = text
    },

    setAttributeObject: (element, name, value) => {
        element.setAttribute(name, value)
    },

    insertObject: (parent, child) => {
        parent.appendChild(child)
    },

    removeObject: element => {
        if (element.parentNode) {
            element.parentNode.removeChild(element)
        }
    }
}
