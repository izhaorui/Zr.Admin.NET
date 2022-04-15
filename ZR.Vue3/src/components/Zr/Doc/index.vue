<template>
  <div>
    <svg-icon icon-class="question" @click="goto"/>
  </div>
</template>
<script setup>
const url = ref('http://www.izhaorui.cn/doc');

function goto() {
  window.open(url.value)
}
</script>