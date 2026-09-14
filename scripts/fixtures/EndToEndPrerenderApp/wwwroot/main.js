import { dotnet } from './_framework/dotnet.js'

// [HYD-4], [HYD-8]: observe every rendered node, excluding only the required state-island removal.
const mount = document.querySelector('#app')
const stateIsland = mount.querySelector('script[data-viu-state]')
globalThis.__viuPrerenderHeading = mount.querySelector('[data-testid="prerender-heading"]')
globalThis.__viuPrerenderState = stateIsland ? JSON.parse(stateIsland.textContent) : null
globalThis.__viuPrerenderMutations = []
const record = mutations => {
    for (const mutation of mutations) {
        if (mutation.type === 'childList' && mutation.addedNodes.length === 0 &&
            mutation.removedNodes.length === 1 && mutation.removedNodes[0] === stateIsland) continue
        globalThis.__viuPrerenderMutations.push(mutation.type)
    }
}
const observer = new MutationObserver(record)
observer.observe(mount, { childList: true, subtree: true, characterData: true, attributes: true })
globalThis.__viuFinishPrerenderProbe = () => {
    record(observer.takeRecords())
    observer.disconnect()
    return globalThis.__viuPrerenderMutations
}

const { runMain, setModuleImports } = await dotnet.create()
setModuleImports('prerender-fixture', {
    markHydrated: () => { globalThis.__viuPrerenderHydrated = true }
})
await runMain()
