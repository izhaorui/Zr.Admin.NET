<template>
  <div>
    <svg-icon icon-class="question" @click="goto"/>
  </div>
</template>

<script>
export default {
  name: 'ZRDoc',
  data() {
    return {
      url: 'http://www.izhaorui.cn/doc'
    }
  },
  methods: {
    goto() {
      window.open(this.url)
    }
  }
}
</script>
