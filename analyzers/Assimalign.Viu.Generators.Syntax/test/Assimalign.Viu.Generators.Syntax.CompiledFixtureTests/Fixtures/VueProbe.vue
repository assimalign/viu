<template>
<aside :class="theme.active">{{ Count }}</aside>
</template>
<script lang="csharp">
public int Count = 3;
</script>
<style module="theme">
.active { font-weight: bold; }
</style>
