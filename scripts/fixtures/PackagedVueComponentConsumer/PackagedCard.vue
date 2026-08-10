<template>
    <article class="packaged-card">{{ Message }}</article>
</template>

<script lang="csharp">
public string Message => "Package-only .vue component";
</script>

<style>
.packaged-card {
    display: block;
}
</style>
