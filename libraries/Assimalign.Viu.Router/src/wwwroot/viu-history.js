// The JS half of the Assimalign.Viu.Router history interop — the browser edge of vue-router's
// HTML5/hash history (createWebHistory / useHistoryStateNavigation / useHistoryListeners:
// https://github.com/vuejs/router/blob/main/packages/router/src/history/html5.ts).
//
// Contract notes:
// - The .NET policy (BrowserRouterHistory) owns every URL and every state object; these functions
//   are dumb appliers. The one thing the DOM owns is the live window scroll saved on the leaving
//   entry during a push (upstream computeScrollPosition()).
// - Reads are batched: readSnapshot() returns the whole location + state in ONE call, so .NET never
//   issues chatty per-property getters ([V01.01.08.02]).
// - State crosses as a flat, primitives-only payload: back/current/forward strings ('' encodes
//   null), a replaced flag and position counter, and an optional { left, top } scroll. No object
//   graph is marshaled.
// - popstate is delivered through the single [JSExport] dispatch, routed by subscription id so
//   teardown (unsubscribe) never leaks a listener across history instances.

let dispatchPopState = null            // the single [JSExport] popstate dispatch
const popstateListeners = new Map()    // subscription id -> popstate handler

// Upstream computeScrollPosition(): the document scroll offset recorded on the leaving entry.
function computeScroll() {
    return { left: window.scrollX, top: window.scrollY }
}

// Reconstruct the flat primitives into the object the History API stores. '' decodes back to null
// so the round-tripped state matches the .NET RouterHistoryState exactly.
function buildStateObject(back, current, forward, replaced, position, scroll) {
    return {
        back: back.length ? back : null,
        current: current,
        forward: forward.length ? forward : null,
        replaced: replaced,
        position: position,
        scroll: scroll
    }
}

export async function initialize() {
    // Bind the single .NET popstate dispatch ([V01.01.08.02]): one JSExport carries the arrived
    // entry's location + state as primitives and routes by subscription id.
    const { getAssemblyExports } = await globalThis.getDotnetRuntime(0)
    const exports = await getAssemblyExports('Assimalign.Viu.Router')
    dispatchPopState = exports.Assimalign.Viu.Router.BrowserHistoryInteropDispatch.DispatchPopState
}

export const history = {
    // Batched read: raw location components + the current entry's state in a single crossing.
    readSnapshot: () => {
        const state = window.history.state
        const hasState = !!(state && typeof state.position === 'number')
        const scroll = hasState && state.scroll ? state.scroll : null
        return [
            window.location.pathname,
            window.location.search,
            window.location.hash,
            window.location.host,
            String(window.history.length),
            hasState ? '1' : '0',
            hasState && state.back ? state.back : '',
            hasState && state.current ? state.current : '',
            hasState && state.forward ? state.forward : '',
            hasState && state.replaced ? '1' : '0',
            hasState ? String(state.position | 0) : '0',
            scroll ? '1' : '0',
            scroll ? String(scroll.left) : '0',
            scroll ? String(scroll.top) : '0'
        ]
    },

    // The document <base> href, or null — the web-mode default when no base is configured.
    readBaseHref: () => {
        const element = document.querySelector('base')
        return element ? element.getAttribute('href') : null
    },

    // Push: rewrite the leaving entry (recording its live scroll) then push the new one — the two
    // changeLocation calls of useHistoryStateNavigation.push, collapsed into one interop crossing.
    push: (currentUrl, amendedBack, amendedCurrent, amendedForward, amendedReplaced, amendedPosition,
           toUrl, newBack, newCurrent, newForward, newReplaced, newPosition,
           newHasScroll, newScrollLeft, newScrollTop) => {
        const amended = buildStateObject(
            amendedBack, amendedCurrent, amendedForward, amendedReplaced, amendedPosition, computeScroll())
        window.history.replaceState(amended, '', currentUrl)
        const created = buildStateObject(
            newBack, newCurrent, newForward, newReplaced, newPosition,
            newHasScroll ? { left: newScrollLeft, top: newScrollTop } : null)
        window.history.pushState(created, '', toUrl)
    },

    // Replace: a single replaceState — the one changeLocation of useHistoryStateNavigation.replace.
    replace: (url, back, current, forward, replaced, position, hasScroll, scrollLeft, scrollTop) => {
        const state = buildStateObject(
            back, current, forward, replaced, position,
            hasScroll ? { left: scrollLeft, top: scrollTop } : null)
        window.history.replaceState(state, '', url)
    },

    go: (delta) => window.history.go(delta),

    // Attach exactly one popstate listener for this history instance. The listener forwards the
    // arrived entry's location + state to the .NET dispatch as primitives, then .NET computes the
    // signed delta/direction against its cached state.
    subscribe: (subscriptionId) => {
        const handler = (event) => {
            if (!dispatchPopState) {
                return
            }
            const state = event.state
            const hasState = !!(state && typeof state.position === 'number')
            const scroll = hasState && state.scroll ? state.scroll : null
            dispatchPopState(
                subscriptionId,
                window.location.pathname,
                window.location.search,
                window.location.hash,
                window.location.host,
                window.history.length,
                hasState,
                hasState ? (state.back ?? null) : null,
                hasState ? (state.current ?? '') : '',
                hasState ? (state.forward ?? null) : null,
                hasState ? !!state.replaced : false,
                hasState ? (state.position | 0) : 0,
                scroll ? true : false,
                scroll ? scroll.left : 0,
                scroll ? scroll.top : 0)
        }
        popstateListeners.set(subscriptionId, handler)
        window.addEventListener('popstate', handler)
    },

    // Deterministic teardown: remove this instance's popstate listener so nothing leaks.
    unsubscribe: (subscriptionId) => {
        const handler = popstateListeners.get(subscriptionId)
        if (handler) {
            window.removeEventListener('popstate', handler)
            popstateListeners.delete(subscriptionId)
        }
    }
}
