<template>
  <div id="app">
    <router-view />
  </div>
</template>

<script>
import { mapGetters } from "vuex";

export default {
  name: "App",
  computed: {
    ...mapGetters(["token"]),
  },
  watch: {
    token: {
      handler: function (val) {
        if (val) {
          this.signalr.start();
        }
      },
      deep: true,
      immediate: true,
    },
  },
};
</script>
