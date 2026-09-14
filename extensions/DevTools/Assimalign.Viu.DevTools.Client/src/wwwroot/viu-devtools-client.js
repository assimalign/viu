// [DVT-14]: browser delivery only; all inspection decisions remain in the Viu application.
const subscriptions = new Map()
let nextIdentifier = 1
const maximumFrameLength = 1024 * 1024

export function subscribe(address, socket, receiver, connected) {
    const identifier = nextIdentifier++
    const subscription = { address, socketTransport: socket, socket: null, timer: null, listener: null, disposed: false }
    subscriptions.set(identifier, subscription)
    if (socket) {
        const connect = () => {
            if (subscription.disposed) return
            subscription.timer = null
            const connection = new WebSocket(address)
            subscription.socket = connection
            const isCurrent = () => !subscription.disposed && subscription.socket === connection
            connection.onopen = () => { if (isCurrent()) connected() }
            connection.onmessage = event => {
                if (!isCurrent() || typeof event.data !== 'string') return
                if (event.data.length > maximumFrameLength) {
                    connection.close(1009, 'The inspection frame exceeds the size limit.')
                    return
                }
                receiver(event.data)
            }
            connection.onerror = () => { if (isCurrent()) connection.close() }
            connection.onclose = () => {
                if (!isCurrent()) return
                subscription.socket = null
                connection.onopen = connection.onmessage = connection.onerror = connection.onclose = null
                if (subscription.timer === null) subscription.timer = setTimeout(connect, 1000)
            }
        }
        connect()
    } else {
        const target = window.parent
        subscription.listener = event => {
            if (subscription.disposed || event.source !== target || (address !== '*' && event.origin !== address)) return
            const frame = event.data
            if (!frame) return
            if (frame.channel === 'assimalign.viu.devtools.ready') { connected(); return }
            if (frame.channel === 'assimalign.viu.devtools.server' && typeof frame.message === 'string' && frame.message.length <= maximumFrameLength) receiver(frame.message)
        }
        window.addEventListener('message', subscription.listener)
        connected()
    }
    return identifier
}

export function send(identifier, message) {
    const subscription = subscriptions.get(identifier)
    if (!subscription || subscription.disposed) throw new Error('The inspection transport is disposed.')
    if (subscription.socketTransport) {
        if (!subscription.socket || subscription.socket.readyState !== WebSocket.OPEN) throw new Error('The inspection socket is disconnected.')
        subscription.socket.send(message)
    } else {
        window.parent.postMessage({ channel: 'assimalign.viu.devtools.client', message }, subscription.address)
    }
}

export function unsubscribe(identifier) {
    const subscription = subscriptions.get(identifier)
    if (!subscription) return
    subscription.disposed = true
    subscriptions.delete(identifier)
    if (subscription.listener) window.removeEventListener('message', subscription.listener)
    if (subscription.timer !== null) {
        clearTimeout(subscription.timer)
        subscription.timer = null
    }
    if (subscription.socket) {
        subscription.socket.onopen = subscription.socket.onmessage = subscription.socket.onerror = subscription.socket.onclose = null
        subscription.socket.close()
    }
}
