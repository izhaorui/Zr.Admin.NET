<template>
  <div class="component-upload-image">
    <el-upload list-type="picture-card" :action="uploadImgUrl" :on-success="handleUploadSuccess" :before-upload="handleBeforeUpload"
      :on-exceed="handleExceed" :on-remove="handleRemove" :on-error="handleUploadError" name="file" :show-file-list="true" :data="data" :limit="limit"
      :file-list="fileList" :on-preview="handlePictureCardPreview" :on-progress="uploadProcess" :class="{hide: this.fileList.length >= this.limit}"
      :headers="headers">
      <i slot="default" class="el-icon-plus"></i>
      <!-- 上传提示 -->
      <div class="el-upload__tip" slot="tip" v-if="showTip">
        请上传
        <template v-if="fileSize"> 大小不超过 <b style="color: #f56c6c">{{ fileSize }}MB</b> </template>
        <template v-if="fileType"> 格式为 <b style="color: #f56c6c">{{ fileType.join("/") }}</b> </template>
        的文件
      </div>
    </el-upload>
    <el-dialog :visible.sync="dialogVisible" title="预览" width="800" append-to-body>
      <img :src="dialogImageUrl" style="display: block; max-width: 100%; margin: 0 auto" />
    </el-dialog>
  </div>
</template>

<script>
import { getToken } from '@/utils/auth'

export default {
  props: {
    value: [String],
    // 图片数量限制
    limit: {
      type: Number,
      default: 1
    },
    column: [String],
    // 上传地址
    uploadUrl: {
      type: String,
      default: process.env.VUE_APP_UPLOAD_URL
    },
    // 文件类型, 例如['png', 'jpg', 'jpeg']
    fileType: {
      type: Array,
      default: () => ['png', 'jpg', 'jpeg', 'webp']
    },
    // 大小限制(MB)
    fileSize: {
      type: Number,
      default: 5
    },
    // 显示手动输入地址
    showInput: false,
    // 是否显示提示
    isShowTip: {
      type: Boolean,
      default: true
    },
    // 上传携带的参数
    data: {
      type: Object
    }
  },
  data() {
    return {
      dialogImageUrl: '',
      dialogVisible: false,
      hideUpload: false,
      uploadImgUrl: process.env.VUE_APP_BASE_API + this.uploadUrl, // 上传的图片服务器地址
      headers: {
        Authorization: 'Bearer ' + getToken()
      },
      imageUrl: '',
      fileList: []
    }
  },
  watch: {
    // 监听 v-model 的值
    value: {
      immediate: true,
      deep: true,
      handler: function(val) {
        if (val) {
          // 首先将值转为数组
          const list = Array.isArray(val) ? val : this.value.split(',')
          // 然后将数组转为对象数组
          this.fileList = list.map((item) => {
            if (typeof item === 'string') {
              // if (item.indexOf(this.baseUrl) === -1) {
              //     item = { name: this.baseUrl + item, url: this.baseUrl + item };
              // } else {
              item = { name: item, url: item }
              // }
            }
            return item
          })
        } else {
          this.fileList = []
          return []
        }
      }
    }
  },
  computed: {
    // 是否显示提示
    showTip() {
      return this.isShowTip && (this.fileType || this.fileSize)
    }
  },
  methods: {
    // 删除图片
    handleRemove(file, fileList) {
      const findex = this.fileList.map((f) => f.name).indexOf(file.name)
      if (findex > -1) {
        this.fileList.splice(findex, 1)
        this.$emit('input', this.column, this.listToString(this.fileList))
      }
    },
    // 上传成功回调
    handleUploadSuccess(res) {
      console.log(res)
      if (res.code != 200) {
        this.msgError(`上传失败，原因:${res.msg}!`)
        return
      }
      this.fileList.push({ name: res.data.fileName, url: res.data.url })
      this.$emit(`input`, this.column, this.listToString(this.fileList))
    },
    // 上传前loading加载
    handleBeforeUpload(file) {
      let isImg = false
      if (this.fileType.length) {
        let fileExtension = ''
        if (file.name.lastIndexOf('.') > -1) {
          fileExtension = file.name.slice(file.name.lastIndexOf('.') + 1)
        }
        isImg = this.fileType.some((type) => {
          if (file.type.indexOf(type) > -1) return true
          if (fileExtension && fileExtension.indexOf(type) > -1) return true
          return false
        })
      } else {
        isImg = file.type.indexOf('image') > -1
      }

      if (!isImg) {
        this.msgError(
          `文件格式不正确, 请上传${this.fileType.join('/')}图片格式文件!`
        )
        return false
      }
      if (this.fileSize) {
        const isLt = file.size / 1024 / 1024 < this.fileSize
        if (!isLt) {
          this.msgError(`上传头像图片大小不能超过 ${this.fileSize} MB!`)
          return false
        }
      }
    },
    // 文件个数超出
    handleExceed() {
      this.$message.error(`上传文件数量不能超过 ${this.limit} 个!`)
    },
    // 预览
    handlePictureCardPreview(file) {
      this.dialogImageUrl = file.url
      this.dialogVisible = true
    },
    // 对象转成指定字符串分隔
    listToString(list, separator) {
      let strs = ''
      separator = separator || ','
      for (const i in list) {
        strs += list[i].url.replace(this.baseUrl, '') + separator
      }
      return strs != '' ? strs.substr(0, strs.length - 1) : ''
    },
    handleUploadError() {
      this.$message({
        type: 'error',
        message: '上传失败'
      })
    },
    // 上传进度
    uploadProcess(event, file, fileList) {
      console.log('上传进度' + file.percentage)
    }
  }
}
</script>

<style scoped lang="scss">
::v-deep.hide .el-upload--picture-card {
  display: none;
}
// 去掉动画效果
::v-deep .el-list-enter-active,
::v-deep .el-list-leave-active {
  transition: all 0s;
}

::v-deep .el-list-enter,
.el-list-leave-active {
  opacity: 0;
  transform: translateY(0);
}
</style>