<template>
  <el-upload :action="uploadImgUrl" :on-success="handleUploadSuccess" :before-upload="handleBeforeUpload" :on-exceed="handleExceed"
    :on-remove="handleRemove" :on-error="handleUploadError" name="file" :show-file-list="true" :limit="limit" :file-list="fileList"
    :on-preview="handlePictureCardPreview" :class="{hide: fileList.length >= limit}" list-type="picture-card" :headers="headers">
    <el-icon class="el-icon--upload">
      <Plus />
    </el-icon>
    <!-- 上传提示 -->
    <template #tip>
      <div class="el-upload__tip">
        请上传
        <template v-if="fileSize">大小不超过<b style="color: #f56c6c">{{ fileSize }}MB</b> </template>
        <template v-if="fileType">格式为 <b style="color: #f56c6c">{{ fileType.join("/") }}</b> </template>
        的文件
      </div>
    </template>
  </el-upload>
  <el-dialog v-model="dialogVisible" title="预览" width="800" append-to-body>
    <img :src="dialogImageUrl" style="display: block; max-width: 100%; margin: 0 auto" />
  </el-dialog>
</template>

<script>
import { getToken } from "@/utils/auth";
import { getCurrentInstance, watch } from "vue";

export default {
  name: "uploadImage",
  emits: ["input"],
  props: {
    modelValue: [String, Object, Array],
    // 图片数量限制
    limit: {
      type: Number,
      default: 1,
    },
    // 大小限制(MB)
    fileSize: {
      type: Number,
      default: 5,
    },
    // 文件类型, 例如['png', 'jpg', 'jpeg']
    fileType: {
      type: Array,
      default: () => ["png", "jpg", "jpeg"],
    },
    // 是否显示提示
    isShowTip: {
      type: Boolean,
      default: true,
    },
    // 上传地址
    uploadUrl: {
      type: String,
      default: import.meta.env.VITE_APP_UPLOAD_URL ?? "/Common/UploadFile",
    },
    column: [String],
  },
  setup(props, ctx) {
    const { proxy } = getCurrentInstance();

    const dialogImageUrl = ref("");
    const dialogVisible = ref(false);
    const baseUrl = import.meta.env.VITE_APP_BASE_API;
    const uploadImgUrl = ref(baseUrl + props.uploadUrl); // 上传的图片服务器地址
    const headers = ref({ Authorization: "Bearer " + getToken() });
    const fileList = ref([]);
    const showTip = computed(
      () => props.isShowTip && (props.fileType || props.fileSize)
    );

    watch(
      () => props.modelValue,
      (val) => {
        if (val) {
          // 首先将值转为数组
          const list = Array.isArray(val) ? val : props.modelValue.split(",");
          // 然后将数组转为对象数组
          fileList.value = list.map((item) => {
            if (typeof item === "string") {
              item = { name: item, url: item };
            }
            return item;
          });
        } else {
          fileList.value = [];
          return [];
        }
      },
      {
        immediate: true,
        deep: true,
      }
    );

    // 删除图片
    function handleRemove(file, files) {
      const findex = fileList.value.map((f) => f.name).indexOf(file.name);
      if (findex > -1) {
        fileList.value.splice(findex, 1);
        ctx.emit("input", props.column, listToString(fileList.value));
      }
    }

    // 上传成功回调
    function handleUploadSuccess(res) {
      console.log(res);
      fileList.value.push({ name: res.data.fileName, url: res.data.url });
      ctx.emit("input", props.column, listToString(fileList.value));
      proxy.$modal.closeLoading();
    }

    // 上传前loading加载
    function handleBeforeUpload(file) {
      let isImg = false;
      if (props.fileType.length) {
        let fileExtension = "";
        if (file.name.lastIndexOf(".") > -1) {
          fileExtension = file.name.slice(file.name.lastIndexOf(".") + 1);
        }
        isImg = props.fileType.some((type) => {
          if (file.type.indexOf(type) > -1) return true;
          if (fileExtension && fileExtension.indexOf(type) > -1) return true;
          return false;
        });
      } else {
        isImg = file.type.indexOf("image") > -1;
      }
      if (!isImg) {
        proxy.$modal.msgError(
          `文件格式不正确, 请上传${props.fileType.join("/")}图片格式文件!`
        );
        return false;
      }
      if (props.fileSize) {
        const isLt = file.size / 1024 / 1024 < props.fileSize;
        if (!isLt) {
          proxy.$modal.msgError(
            `上传头像图片大小不能超过 ${props.fileSize} MB!`
          );
          return false;
        }
      }
      proxy.$modal.loading("上传中");
    }

    // 文件个数超出
    function handleExceed() {
      proxy.$modal.msgError(`上传文件数量不能超过 ${props.limit} 个!`);
    }

    // 上传失败
    function handleUploadError() {
      proxy.$modal.msgError("上传失败");
      proxy.$modal.closeLoading();
    }

    // 预览
    function handlePictureCardPreview(file) {
      dialogImageUrl.value = file.url;
      dialogVisible.value = true;
    }

    // 对象转成指定字符串分隔
    function listToString(list, separator) {
      let strs = "";
      separator = separator || ",";
      for (let i in list) {
        strs += list[i].url.replace(baseUrl, "") + separator;
      }
      return strs != "" ? strs.substr(0, strs.length - 1) : "";
    }

    return {
      dialogImageUrl,
      dialogVisible,
      uploadImgUrl,
      headers,
      fileList,
      showTip,
      handleRemove,
      handleUploadSuccess,
      handleBeforeUpload,
      handleExceed,
      handleUploadError,
      handlePictureCardPreview,
    };
  },
};
</script>