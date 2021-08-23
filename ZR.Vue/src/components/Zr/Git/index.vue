<template>
  <div>
    <svg-icon icon-class="github" @click="goto"/>
  </div>
</template>

<script>
export default {
  name: 'IZoryGit',
  data() {
    return {
      url: 'https://gitee.com/izhaorui/ZrAdminNetCore'
    }
  },
  methods: {
    goto() {
      window.open(this.url)
    }
  }
}
</script>
