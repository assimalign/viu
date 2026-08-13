// Browser transport for the optional Assimalign.Viu.DevTools package. A complete protocol batch
// crosses the managed/JavaScript boundary as one string. Inbound and outbound directions use
// distinct channel names so a window never consumes its own server frame.

let dispatch = null
const subscriptions = new Map()

export async function initialize() {
    const { getAssemblyExports } = await globalThis.getDotnetRuntime(0)
    const exports = await getAssemblyExports('Assimalign.Viu.DevTools')
    dispatch = exports.Assimalign.Viu.DevTools.PostMessageInteropDispatch.Dispatch
}

export const postMessage = {
    subscribe: (subscriptionIdentifier, targetOrigin) => {
        const listener = (event) => {
            const frame = event.data
            if (!dispatch
                || !frame
                || frame.channel !== 'assimalign.viu.devtools.client'
                || typeof frame.message !== 'string'
                || (targetOrigin !== '*' && event.origin !== targetOrigin)) {
                return
            }

            dispatch(subscriptionIdentifier, frame.message)
        }

        subscriptions.set(subscriptionIdentifier, { listener, targetOrigin })
        window.addEventListener('message', listener)
    },

    unsubscribe: (subscriptionIdentifier) => {
        const subscription = subscriptions.get(subscriptionIdentifier)
        if (subscription) {
            window.removeEventListener('message', subscription.listener)
            subscriptions.delete(subscriptionIdentifier)
        }
    },

    send: (subscriptionIdentifier, message) => {
        const subscription = subscriptions.get(subscriptionIdentifier)
        if (!subscription) {
            return
        }

        window.postMessage(
            { channel: 'assimalign.viu.devtools.server', message },
            subscription.targetOrigin)
    }
}
