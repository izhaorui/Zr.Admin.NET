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
      url: 'https://gitee.com/izory/ZrAdminNetCore'
    }
  },
  methods: {
    goto() {
      window.open(this.url)
    }
  }
}
</script>
