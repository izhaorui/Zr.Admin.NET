<template>
  <el-row>
    <el-form class="mt10" ref="form" :model="form" label-width="110px" :rules="rules" style="width:600px">
      <el-form-item v-for="(domain, index) in form.toEmails" :prop="'toEmails.' + index + '.value'" :label="'收件邮箱' + (index === 0 ? '': index)"
        :key="domain.key"
        :rules="[{ required: true, message: '邮箱不能为空', trigger: 'blur' }, { message: '请输入正确的邮箱地址', trigger: ['blur', 'change'], type: 'email', }]">
        <el-input v-model="domain.value" style="width:300px"></el-input>
        <el-button class="ml10" @click="addDomain" icon="el-icon-plus" />
        <el-button class="ml10" @click.prevent="removeDomain(domain)" icon="el-icon-minus" />
      </el-form-item>
      <el-form-item label="邮件主题" prop="subject">
        <el-input v-model="form.subject"></el-input>
      </el-form-item>
      <el-form-item label="邮件内容" prop="htmlContent">
        <editor v-model="form.htmlContent" :min-height="192" />
      </el-form-item>
      <el-form-item label="发送自己" prop="sendMe">
        <el-switch v-model="form.sendMe" active-text="是" inactive-text="否"></el-switch>
      </el-form-item>
      <el-form-item label="附件">
        <UploadFile v-model="form.fileUrl" :limit="5" :fileSize="15" :data="{ 'fileDir' : 'email', 'uploadType': 1}" column="fileUrl"
          @input="uploadSuccess" />
      </el-form-item>
      <el-form-item>
        <el-button type="primary" size="mini" @click="formSubmit">发送邮件</el-button>
      </el-form-item>
    </el-form>
  </el-row>
</template>
<script>
import { sendEmail } from "@/api/common";

export default {
  name: "sendEmail",
  data() {
    return {
      form: {
        fileUrl: "",
        toEmails: [
          {
            value: "",
          },
        ],
      },
      uploadActionUrl: process.env.VUE_APP_BASE_API + "/common/uploadFile",
      rules: {
        subject: [{ required: true, message: "主题不能为空", trigger: "blur" }],
        content: [{ required: true, message: "内容不能为空", trigger: "blur" }],
      },
    };
  },
  methods: {
    // 表单重置
    reset() {
      this.form = {
        toUser: undefined,
        htmlContent: undefined,
        subject: undefined,
        fileUrl: undefined,
        sendMe: false,
        toEmails: [
          {
            value: "",
          },
        ],
      };
      this.resetForm("form");
    },
    // 上传成功
    uploadSuccess(columnName, filelist) {
      this.form[columnName] = filelist;
    },
    /**
     * 提交
     */
    formSubmit() {
      this.$refs["form"].validate((valid) => {
        //开启校验
        if (valid) {
          const loading = this.$loading({
            lock: true,
            text: "Loading",
            spinner: "el-icon-loading",
            background: "rgba(0, 0, 0, 0.7)",
          });
          var emails = [];
          this.form.toEmails.filter((x) => {
            emails.push(x.value);
          });
          var p = {
            ...this.form,
            toUser: emails.toString(),
          };
          // 如果校验通过，请求接口，允许提交表单
          sendEmail(p).then((res) => {
            this.open = false;
            if (res.code == 200) {
              this.$message.success("发送成功");
              this.reset();
            }
            loading.close();
          });
          setTimeout(() => {
            loading.close();
          }, 5000);
        } else {
          console.log("未通过");
          //校验不通过
          return false;
        }
      });
    },
    removeDomain(item) {
      var index = this.form.toEmails.indexOf(item);
      if (index !== -1 && this.form.toEmails.length !== 1) {
        this.form.toEmails.splice(index, 1);
      } else {
        this.$message({
          message: "请至少保留一位联系人",
          type: "warning",
        });
      }
    },
    addDomain() {
      this.form.toEmails.push({
        value: "",
        key: Date.now(),
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
