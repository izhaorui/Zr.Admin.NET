<template>
  <div :class="className" :style="{height:height,width:width}" />
</template>

<script>
import * as echarts from "echarts";

require("echarts/theme/macarons"); // echarts theme
import { debounce } from "@/utils";

export default {
  props: {
    name: {
      type: String,
      default: "业务指标",
    },
    min: {
      type: [Object, Number],
      default: 0,
    },
    max: {
      type: [Object, Number],
      default: 0,
    },
    data: {
      type: Array,
      default: () => [
        {
          value: "",
          name: "占比",
        },
      ],
    },
    className: {
      type: String,
      default: "chart",
    },
    width: {
      type: String,
      default: "100%",
    },
    height: {
      type: String,
      default: "300px",
    },
  },
  data() {
    return {
      chart: null,
    };
  },
  mounted() {
    this.initChart();
    this.__resizeHandler = debounce(() => {
      if (this.chart) {
        this.chart.resize();
      }
    }, 100);
    window.addEventListener("resize", this.__resizeHandler);
  },
  beforeDestroy() {
    if (!this.chart) {
      return;
    }
    window.removeEventListener("resize", this.__resizeHandler);
    this.chart.dispose();
    this.chart = null;
  },
  methods: {
    initChart() {
      this.chart = echarts.init(this.$el, "macarons");

      this.chart.setOption({
        tooltip: {
          formatter: "{a} <br/>{b} : {c}",
        },
        // toolbox: {
        //   feature: {
        //     restore: {},
        //     saveAsImage: {},
        //   },
        // },
        series: [
          {
            name: this.name,
            type: "gauge",
            min: this.min,
            max: this.max,
            detail: {
              formatter: "{value}%",
            },
            data: this.data,
          },
        ],
      });
    },
  },
};
</script>
