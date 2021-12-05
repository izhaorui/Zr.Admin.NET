<template>
  <div class="component-upload-image">
    <el-upload :action="uploadImgUrl" :on-success="handleUploadSuccess" :before-upload="handleBeforeUpload" :on-error="handleUploadError" name="file"
      :show-file-list="false" :headers="headers" style="display: inline-block; vertical-align: top">
      <el-image v-if="imageUrl" :src="imageUrl" class="icon" />
      <i v-else class="el-icon-plus uploader-icon"></i>
    </el-upload>
  </div>
</template>

<script>
import { getToken } from "@/utils/auth";

export default {
  name: "UploadImage",
  data() {
    return {
      uploadImgUrl: process.env.VUE_APP_BASE_API + this.uploadUrl, // 上传的图片服务器地址
      headers: {
        Authorization: "Bearer " + getToken(),
      },
      imageUrl: "",
    };
  },
  props: {
    icon: {
      type: String,
    },
    // 当前form 列名
    column: { type: String },
    // 上传地址
    uploadUrl: {
      type: String,
      default: "Common/UploadFile",
    },
    // 文件类型, 例如['png', 'jpg', 'jpeg']
    fileType: {
      type: Array,
      default: () => ["png", "jpg", "jpeg"],
    },
    // 大小限制(MB)
    fileSize: {
      type: Number,
      default: 5,
    },
  },
  mounted() {
    this.imageUrl = this.icon;
  },
  methods: {
    handleUploadSuccess(res) {
      this.$emit(`handleUploadSuccess`, res, this.column);
      this.imageUrl = res.data;
      this.loading.close();
    },
    // 上传前loading加载
    handleBeforeUpload(file) {
			console.log(file)
      let isImg = false;
      if (this.fileType.length) {
        let fileExtension = "";
        if (file.name.lastIndexOf(".") > -1) {
          fileExtension = file.name.slice(file.name.lastIndexOf(".") + 1);
        }
        isImg = this.fileType.some((type) => {
          if (file.type.indexOf(type) > -1) return true;
          if (fileExtension && fileExtension.indexOf(type) > -1) return true;
          return false;
        });
      } else {
        isImg = file.type.indexOf("image") > -1;
      }

      if (!isImg) {
        this.msgError(
          `文件格式不正确, 请上传${this.fileType.join("/")}图片格式文件!`
        );
        return false;
      }
      if (this.fileSize) {
        const isLt = file.size / 1024 / 1024 < this.fileSize;
        if (!isLt) {
          this.msgError(`上传头像图片大小不能超过 ${this.fileSize} MB!`);
          return false;
        }
      }
      this.loading = this.$loading({
        lock: true,
        text: "上传中",
        background: "rgba(0, 0, 0, 0.7)",
      });
    },
    handleUploadError() {
      this.$message({
        type: "error",
        message: "上传失败",
      });
      this.loading.close();
    },
  },
  watch: {},
};
</script>

<style scoped lang="scss">
.avatar {
  width: 100%;
  height: 100%;
}
</style>