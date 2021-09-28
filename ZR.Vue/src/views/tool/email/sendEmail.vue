<template>
  <el-row>
    <el-form class="mt10" ref="form" :model="form" label-width="100px" :rules="rules">
      <el-form-item label="收件邮箱" prop="toUser">
        <el-input v-model="form.toUser">
        </el-input>
      </el-form-item>
      <el-form-item label="邮件主题" prop="subject">
        <el-input v-model="form.subject"></el-input>
      </el-form-item>
      <el-form-item label="邮件内容" prop="content">
        <el-input v-model="form.content" rows="3" type="textarea"></el-input>
      </el-form-item>

      <el-form-item label="附件">
        <el-upload name="file" ref="upload" :data="{savetype: form.saveType, filePath: form.filePath}" :headers="headers" :auto-upload="false" :on-success="uploadSuccess" :action="uploadActionUrl">
          <el-button slot="trigger" size="mini" icon="el-icon-upload">选择文件</el-button>
          <el-button style="margin-left: 10px;" size="mini" type="primary" @click="submitUpload">上传到服务器</el-button>
        </el-upload>
      </el-form-item>
      <el-form-item>
        <el-button type="primary" size="mini" @click="formSubmit">立即提交</el-button>
      </el-form-item>
    </el-form>
  </el-row>
</template>
<script>
import { sendEmail } from "@/api/common";
import { getToken } from "@/utils/auth";

export default {
  name: "sendEmail",
  data() {
    return {
      form: {
        fileUrl: "",
      },
      headers: {
        Token: "",
      },
      uploadActionUrl: process.env.VUE_APP_BASE_API + "upload/SaveFile",
      rules: {
        subject: [{ required: true, message: "主题不能为空", trigger: "blur" }],
        toUser: [
          { required: true, message: "请输入邮箱地址", trigger: ["blur"] },
          {
            message: "请输入正确的邮箱地址",
            trigger: ["blur", "change"],
            type: "email",
          },
        ],
        content: [{ required: true, message: "内容不能为空", trigger: "blur" }],
      },
    };
  },
  mounted() {
    console.log(getToken());
    this.headers.Token = getToken();
  },
  methods: {
    // 表单重置
    reset() {
      this.form = {
        toUser: undefined,
        content: undefined,
        subject: undefined,
        fileUrl: undefined,
      };
      this.resetForm("form");
    },
    submitUpload() {
      this.$refs.upload.submit();
    },
    // 上传成功
    uploadSuccess(response, file, fileList) {
      console.log(response);
      if (response.code == 200) {
        this.$message.success("上传成功");
        this.form.fileUrl = response.data.fullPath;
      } else {
        this.$message.error(response.msg);
      }
    },
    formSubmit() {
      console.log(JSON.stringify(this.form));
      this.$refs["form"].validate((valid) => {
        //开启校验
        if (valid) {
          const loading = this.$loading({
            lock: true,
            text: "Loading",
            spinner: "el-icon-loading",
            background: "rgba(0, 0, 0, 0.7)",
          });
          // 如果校验通过，请求接口，允许提交表单
          sendEmail(this.form).then((res) => {
            this.open = false;
            if (res.code == 200) {
              this.$message.success("发送成功");
              this.reset();
            }
            this.$refs.upload.clearFiles();
            loading.close();
          });
          setTimeout(() => {
            loading.close();
          }, 5000);
        } else {
          //校验不通过
          return false;
        }
      });
    },
  },
};
</script>
<style scoped>
.el-upload-list {
  width: 200px;
}
</style>
