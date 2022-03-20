<template>
  <div class="app-container">
    <el-row :gutter="24">
      <!-- :model属性用于表单验证使用 比如下面的el-form-item 的 prop属性用于对表单值进行验证操作 -->
      <el-form :model="form" label-position="right" ref="form" size="mini" label-width="100px" :rules="rules" @submit.native.prevent>
        <el-col :lg="12">
          <el-form-item label="文章标题" prop="title">
            <el-input v-model="form.title" placeholder="请输入文章标题（必须）" />
          </el-form-item>
        </el-col>
        <el-col :lg="12">
          <el-form-item label="文章分类" prop="category_id">
            <treeselect v-model="form.category_id" :options="categoryOptions" :normalizer="normalizer" :show-count="true" />
          </el-form-item>
        </el-col>
        <el-col :lg="24">
          <el-form-item label="文章标签">
            <el-tag size="large" :key="tag" v-for="tag in form.dynamicTags" closable :disable-transitions="false" @close="handleCloseTag(tag)">
              {{tag}}
            </el-tag>
            <el-input class="input-new-tag" v-if="inputVisible" v-model="inputValue" ref="saveTagInput" size="small"
              @keyup.enter.native="handleInputConfirm" @blur="handleInputConfirm">
            </el-input>
            <el-button v-else class="button-new-tag" size="small" @click="showInput">+ 文章标签</el-button>
          </el-form-item>
        </el-col>
        <el-col :lg="24">
          <el-form-item prop="content" label="文章内容">
            <mavon-editor v-model="form.content" ref="md" @imgAdd="$imgAdd" @change="change" style="min-height: 400px;width:100%" />
          </el-form-item>
        </el-col>
        <el-col :lg="24">
          <el-form-item label="" style="text-align:right;">
            <el-button size="mini" @click="handlePublish('1')">发布文章</el-button>
            <el-button type="success" size="mini" @click="handlePublish('2')">存为草稿</el-button>
          </el-form-item>
        </el-col>

      </el-form>
    </el-row>
  </div>
</template>
<script>
import {
  addArticle,
  updateArticle,
  listArticleCategoryTree,
  getArticle,
} from "@/api/system/article.js";
import { upload } from "@/api/common.js";
import { mavonEditor } from "mavon-editor";
import "mavon-editor/dist/css/index.css";
import Treeselect from "@riophae/vue-treeselect";
import "@riophae/vue-treeselect/dist/vue-treeselect.css";

export default {
  name: "articlepublish",
  components: {
    mavonEditor,
    Treeselect,
  },
  data() {
    return {
      // 表单参数
      form: {
        dynamicTags: [],
        fmt_type: "markdown",
        tags: undefined,
        cid: undefined,
        content: undefined,
      },
      // 文章目录下拉框
      categoryOptions: [],
      // 提交按钮是否显示
      btnSubmitVisible: true,
      inputVisible: false,
      inputValue: "",
      // 表单校验
      rules: {
        title: [{ required: true, message: "标题不能为空", trigger: "blur" }],
        content: [{ required: true, message: "内容不能为空", trigger: "blur" }],
      },
    };
  },
  watch: {
    "form.cid": function (newVal, oldVal) {
      getArticle(newVal).then((res) => {
        if (res.code == 200) {
          var data = res.data;
          this.form = {
            fmt_type: data.fmt_type,
            cid: parseInt(newVal),
            title: data.title,
            content: data.content,
            category_id: data.category_id,
            dynamicTags: data.tags != null && data.tags.length > 0 ? data.tags.split(",") : [],
          };
        }
      });
    },
  },
  created() {
    this.form.cid = this.$route.query.cid;
    this.getCategoryTreeselect();
  },
  mounted() {},
  methods: {
    /** 查询菜单下拉树结构 */
    getCategoryTreeselect() {
      listArticleCategoryTree().then((res) => {
        if (res.code == 200) {
          this.categoryOptions = res.data;
        }
      });
    },
    /** 转换菜单数据结构 */
    normalizer(node) {
      if (node.children && !node.children.length) {
        delete node.children;
      }
      return {
        id: node.category_Id,
        label: node.name,
        children: node.children,
      };
    },
    // 将图片上传到服务器，返回地址替换到md中
    $imgAdd(pos, $file) {
      var formdata = new FormData();
      formdata.append("file", $file);
      upload(formdata).then((res) => {
        console.log(JSON.stringify(res));
        this.$refs.md.$img2Url(pos, res.data.url);
      });
    },
    change(value, render) {
      // render 为 markdown 解析后的结果
      this.html = render;
    },
    /** 提交按钮 */
    handlePublish: function (status) {
      this.form.status = status;
      this.form.tags = this.form.dynamicTags.toString();

      this.$refs["form"].validate((valid) => {
        if (valid) {
          if (this.form.cid != undefined) {
            updateArticle(this.form).then((res) => {
              if (res.code == 200) {
                this.msgSuccess("修改文章成功");
                this.$tab.closeOpenPage({ path: '/tool/article/index' });
              } else {
                this.msgError("修改文章失败");
              }
            });
          } else {
            addArticle(this.form).then((res) => {
              if (res.code == 200) {
                this.msgSuccess("发布文章成功");
                this.$tab.closeOpenPage({ path: '/tool/article/index' });
              } else {
                this.msgError("发布文章失败");
              }
            });
          }
        }
      });
    },
    handleCloseTag(tag) {
      this.form.dynamicTags.splice(this.form.dynamicTags.indexOf(tag), 1);
    },
    showInput() {
      this.inputVisible = true;
      this.$nextTick((_) => {
        this.$refs.saveTagInput.$refs.input.focus();
      });
    },
    handleInputConfirm() {
      let inputValue = this.inputValue.trim();

      if (
        inputValue &&
        inputValue.length > 0 &&
        this.form.dynamicTags.length < 4
      ) {
        this.form.dynamicTags.push(inputValue);
      }
      this.inputVisible = false;
      this.inputValue = "";
    },
    handleTreeSelect() {},
  },
};
</script>
<style scoped>
.el-tag + .el-tag {
  margin-right: 10px;
}
.el-tag {
  margin-right: 10px;
}
.button-new-tag {
  /* margin-left: 10px; */
  height: 32px;
  line-height: 30px;
  padding-top: 0;
  padding-bottom: 0;
}
.input-new-tag {
  width: 90px;
  margin-right: 10px;
  vertical-align: bottom;
}
.el-col {
  border-radius: 4px;
  margin-bottom: 10px;
}
.vue-treeselect {
  z-index: 1501;
}
</style>
