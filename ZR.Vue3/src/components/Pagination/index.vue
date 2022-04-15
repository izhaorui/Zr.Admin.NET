<template>
  <div :class="{ 'hidden': hidden }" class="pagination-container">
    <el-pagination small background v-model:current-page="currentPage" v-model:page-size="pageSize" :layout="layout" :page-sizes="pageSizes"
      :pager-count="pagerCount" :total="total" @size-change="handleSizeChange" @current-change="handleCurrentChange" />
  </div>
</template>

<script>
// import { scrollTo } from "@/utils/scroll-to";
import { computed } from "vue";
export default {
  name: "pagingation",
  emits: ["update:page", "update:limit", "pagination"],
  props: {
    total: {
      required: true,
      type: Number,
    },
    page: {
      type: Number,
      default: 1,
    },
    limit: {
      type: Number,
      default: 20,
    },
    pageSizes: {
      type: Array,
      default() {
        return [10, 20, 30, 50];
      },
    },
    // 移动端页码按钮的数量端默认值5
    pagerCount: {
      type: Number,
      default: document.body.clientWidth < 992 ? 5 : 7,
    },
    layout: {
      type: String,
      default: "total, sizes, prev, pager, next, jumper",
    },
    background: {
      type: Boolean,
      default: true,
    },
    autoScroll: {
      type: Boolean,
      default: true,
    },
    hidden: {
      type: Boolean,
      default: false,
    },
  },
  setup(props, {ctx, emit }) {
		
    const currentPage = computed({
      get() {
        return props.page;
      },
      set(val) {
        emit("update:page", val);
      },
    });
    const pageSize = computed({
      get() {
        return props.limit;
      },
      set(val) {
        emit("update:limit", val);
      },
    });

    function handleSizeChange(val) {
      emit("pagination", { page: currentPage.value, limit: val });
      if (props.autoScroll) {
        // scrollTo(0, 800);
      }
    }
    function handleCurrentChange(val) {
			console.log(val)
      emit("pagination", { page: val, limit: pageSize.value });
      if (props.autoScroll) {
        // scrollTo(0, 800);
      }
    }

    return {
			currentPage,
			pageSize,
      handleSizeChange,
      handleCurrentChange,
    };
  },
};
</script>
<style scoped>
.pagination-container {
  /* background: #fff; */
  padding: 32px 16px;
}
.pagination-container.hidden {
  display: none;
}
</style>