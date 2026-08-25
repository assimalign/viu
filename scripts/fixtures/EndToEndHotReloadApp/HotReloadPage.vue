<template>
    <main class="hot-shell" data-testid="hot-shell">
        <h1 data-testid="hot-heading">Hot reload template v1</h1>
        <div data-testid="hot-removable">Structural removal baseline</div>
        <p data-testid="hot-count">{{ Count }}</p>
        <button type="button" data-testid="hot-increment" @click="Increment">
            Increment
        </button>
        <div :class="UtilityClasses" data-testid="utility-probe">
            <span data-testid="utility-style-probe">Utility stylesheet probe</span>
        </div>
    </main>
</template>

<script lang="csharp">
using Assimalign.Viu;
using Assimalign.Viu.Reactivity;

private readonly Reference<int> CountReference = Reactive.Reference(0);

public int Count => CountReference.Value;

public string UtilityClasses => "hidden";

private void Increment() => CountReference.Value++;
</script>

<style scoped>
.hot-shell {
    display: grid;
    gap: 0.5rem;
}
</style>
