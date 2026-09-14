// [DVT-6], [DVT-14]: deterministic host-adapter tests; no browser package or real timer required.
import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'

const source = await readFile(new URL('../src/wwwroot/viu-devtools-client.js', import.meta.url), 'utf8')
const transport = await import(`data:text/javascript;base64,${Buffer.from(source).toString('base64')}`)
const origin = 'https://inspected.example'
const completeBatch = '{"protocol":"assimalign.viu.devtools","messages":[{"version":1,"type":"tree.snapshot","payload":{"roots":[]}},{"version":1,"type":"timeline.dropped","payload":{"count":2}}]}'
let passed = 0

function run(name, action) {
    action()
    passed++
    process.stdout.write(`PASS ${name}\n`)
}

function createWindow() {
    const listeners = new Set()
    const sent = []
    const parent = { postMessage: (frame, targetOrigin) => sent.push({ frame, targetOrigin }) }
    const host = {
        parent,
        addEventListener: (name, listener) => {
            assert.equal(name, 'message')
            listeners.add(listener)
        },
        removeEventListener: (name, listener) => {
            assert.equal(name, 'message')
            listeners.delete(listener)
        }
    }
    globalThis.window = host
    return {
        parent, listeners, sent,
        dispatch: (data, source = parent, eventOrigin = origin) => {
            for (const listener of listeners) listener({ data, source, origin: eventOrigin })
        }
    }
}

run('postMessage filters source, origin, direction, shape and oversized frames', () => {
    const host = createWindow()
    const received = []
    let connections = 0
    const identifier = transport.subscribe(origin, false, message => received.push(message), () => connections++)
    assert.equal(connections, 1)
    const server = { channel: 'assimalign.viu.devtools.server', message: completeBatch }
    host.dispatch(server, {}, origin)
    host.dispatch(server, host.parent, 'https://another.example')
    host.dispatch({ channel: 'assimalign.viu.devtools.client', message: completeBatch })
    host.dispatch({ channel: 'assimalign.viu.devtools.server', message: {} })
    host.dispatch({ channel: 'assimalign.viu.devtools.server', message: 'x'.repeat(1024 * 1024 + 1) })
    host.dispatch(null)
    assert.deepEqual(received, [])
    host.dispatch(server)
    assert.deepEqual(received, [completeBatch])
    transport.unsubscribe(identifier)
    assert.equal(host.listeners.size, 0)
})

run('postMessage sends one unchanged batch to the inspected parent and exact origin', () => {
    const host = createWindow()
    const identifier = transport.subscribe(origin, false, () => {}, () => {})
    transport.send(identifier, completeBatch)
    assert.deepEqual(host.sent, [{
        frame: { channel: 'assimalign.viu.devtools.client', message: completeBatch },
        targetOrigin: origin
    }])
    transport.unsubscribe(identifier)
    transport.unsubscribe(identifier)
    assert.throws(() => transport.send(identifier, completeBatch), /disposed/)
})

run('runtime readiness reconnects only from the pinned parent and origin', () => {
    const host = createWindow()
    let connections = 0
    const identifier = transport.subscribe(origin, false, () => {}, () => connections++)
    const ready = { channel: 'assimalign.viu.devtools.ready' }
    host.dispatch(ready, {}, origin)
    host.dispatch(ready, host.parent, 'https://another.example')
    assert.equal(connections, 1)
    host.dispatch(ready)
    assert.equal(connections, 2)
    transport.unsubscribe(identifier)
    host.dispatch(ready)
    assert.equal(connections, 2)
})

run('explicit wildcard origin still requires the selected parent source', () => {
    const host = createWindow()
    const received = []
    const identifier = transport.subscribe('*', false, message => received.push(message), () => {})
    const server = { channel: 'assimalign.viu.devtools.server', message: completeBatch }
    host.dispatch(server, {}, 'https://another.example')
    host.dispatch(server, host.parent, 'https://another.example')
    assert.deepEqual(received, [completeBatch])
    transport.unsubscribe(identifier)
})

function createSockets() {
    const connections = []
    const timers = new Map()
    let nextTimer = 1
    globalThis.setTimeout = (action, milliseconds) => {
        assert.equal(milliseconds, 1000)
        const identifier = nextTimer++
        timers.set(identifier, action)
        return identifier
    }
    globalThis.clearTimeout = identifier => timers.delete(identifier)
    class FakeWebSocket {
        static OPEN = 1
        readyState = 0
        messages = []
        closeCount = 0
        closeCode = null
        constructor(address) {
            this.address = address
            connections.push(this)
        }
        send(message) { this.messages.push(message) }
        close(code = null) {
            this.closeCount++
            this.closeCode = code
            this.readyState = 3
            this.onclose?.()
        }
        open() {
            this.readyState = FakeWebSocket.OPEN
            this.onopen?.()
        }
    }
    globalThis.WebSocket = FakeWebSocket
    return {
        connections, timers,
        retry: () => {
            assert.equal(timers.size, 1)
            const [identifier, action] = timers.entries().next().value
            timers.delete(identifier)
            action()
        }
    }
}

run('WebSocket open negotiates; send and receive preserve complete text batches', () => {
    const host = createSockets()
    const received = []
    let connectionCount = 0
    const identifier = transport.subscribe('wss://inspected.example/socket', true,
        message => received.push(message), () => connectionCount++)
    const connection = host.connections[0]
    assert.equal(connection.address, 'wss://inspected.example/socket')
    assert.equal(connectionCount, 0)
    assert.throws(() => transport.send(identifier, completeBatch), /disconnected/)
    connection.open()
    assert.equal(connectionCount, 1)
    transport.send(identifier, completeBatch)
    assert.deepEqual(connection.messages, [completeBatch])
    connection.onmessage({ data: completeBatch })
    connection.onmessage({ data: new Uint8Array([1, 2]) })
    connection.onmessage({ data: 'x'.repeat(1024 * 1024 + 1) })
    assert.deepEqual(received, [completeBatch])
    assert.equal(connection.closeCode, 1009)
    assert.equal(host.timers.size, 1)
    transport.unsubscribe(identifier)
    assert.equal(connection.closeCount, 1)
    assert.equal(connection.onopen, null)
    assert.equal(connection.onmessage, null)
    assert.equal(connection.onerror, null)
    assert.equal(connection.onclose, null)
    assert.equal(host.timers.size, 0)
})

run('WebSocket close retries once, and replacement open starts another handshake', () => {
    const host = createSockets()
    let connectionCount = 0
    const identifier = transport.subscribe('wss://inspected.example/socket', true,
        () => {}, () => connectionCount++)
    host.connections[0].open()
    host.connections[0].close()
    assert.equal(host.timers.size, 1)
    host.retry()
    assert.equal(host.connections.length, 2)
    host.connections[1].open()
    assert.equal(connectionCount, 2)
    transport.unsubscribe(identifier)
    assert.equal(host.timers.size, 0)
})

run('WebSocket error closes the connection, and disposal cancels pending retry', () => {
    const host = createSockets()
    const identifier = transport.subscribe('wss://inspected.example/socket', true, () => {}, () => {})
    host.connections[0].onerror()
    assert.equal(host.connections[0].closeCount, 1)
    assert.equal(host.timers.size, 1)
    transport.unsubscribe(identifier)
    assert.equal(host.timers.size, 0)
    assert.throws(() => transport.send(identifier, completeBatch), /disposed/)
})

run('closed socket callbacks cannot deliver stale frames, handshakes, or duplicate retries', () => {
    const host = createSockets()
    const received = []
    let connectionCount = 0
    const identifier = transport.subscribe('wss://inspected.example/socket', true,
        message => received.push(message), () => connectionCount++)
    const previous = host.connections[0]
    const previousOpen = previous.onopen
    const previousMessage = previous.onmessage
    const previousClose = previous.onclose
    const previousError = previous.onerror
    previous.open()
    previous.close()
    assert.equal(previous.onmessage, null)
    assert.throws(() => transport.send(identifier, completeBatch), /disconnected/)
    previousOpen()
    previousMessage({ data: completeBatch })
    previousClose()
    previousError()
    assert.equal(connectionCount, 1)
    assert.deepEqual(received, [])
    assert.equal(host.timers.size, 1)
    host.retry()
    host.connections[1].open()
    previousOpen()
    previousMessage({ data: completeBatch })
    previousClose()
    previousError()
    assert.equal(connectionCount, 2)
    assert.deepEqual(received, [])
    assert.equal(host.timers.size, 0)
    transport.send(identifier, completeBatch)
    assert.deepEqual(host.connections[1].messages, [completeBatch])
    transport.unsubscribe(identifier)
})

process.stdout.write(`${passed} browser transport checks passed.\n`)
