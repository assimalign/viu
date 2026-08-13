<template>
    <main class="hot-shell" data-testid="hot-shell">
        <h1 data-testid="hot-heading">Hot reload template v1</h1>
        <p data-testid="hot-count">{{ Count }}</p>
        <button type="button" data-testid="hot-increment" @click="Increment">
            Increment
        </button>
    </main>
</template>

<script lang="csharp">
using Assimalign.Viu;
using Assimalign.Viu.Reactivity;

private readonly Reference<int> CountReference = Reactive.Reference(0);

public int Count => CountReference.Value;

private void Increment() => CountReference.Value++;
</script>

<style scoped>
.hot-shell {
    display: grid;
    gap: 0.5rem;
}
</style>
