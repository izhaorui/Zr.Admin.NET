<template>
  <div class="app-container">
    <el-row :gutter="24">
      <!-- :model属性用于表单验证使用 比如下面的el-form-item 的 prop属性用于对表单值进行验证操作 -->
      <el-form :model="queryParams" label-position="left" inline ref="queryForm" v-show="showSearch" @submit.native.prevent>
        <el-col :span="8">
          <el-form-item label="存储位置" prop="saveStore">
            <el-select v-model="queryParams.saveStore" placeholder="请选择存储位置">
              <el-option v-for="dict in options" :key="dict.dictValue" :label="dict.dictLabel" :value="dict.dictValue" />
            </el-select>
          </el-form-item>
        </el-col>
        <el-col :span="8">
          <el-form-item label="文件仓库" prop="savePath">
            <el-input v-model="queryParams.savePath" placeholder="请输入文件仓库" />
          </el-form-item>
        </el-col>
        <el-col :span="8">
          <el-form-item label="文件名" prop="fileName">
            <el-input v-model="queryParams.fileName" placeholder="请输入文件名称"></el-input>
          </el-form-item>
        </el-col>

        <el-col :span="24" style="text-align:center;">
          <el-form-item>
            <el-button type="cyan" icon="el-icon-search" size="mini" @click="handleQuery">查询</el-button>
            <el-button icon="el-icon-refresh" size="mini" @click="resetQuery">重置</el-button>
          </el-form-item>
        </el-col>

      </el-form>
    </el-row>

    <!-- 工具区域 -->
    <el-row :gutter="10" class="mb8">
      <el-col :span="1.5">
        <el-upload size="mini" :action="uploadUrl" :on-change="handleChange">上传文件</el-upload>
      </el-col>
      <right-toolbar :showSearch.sync="showSearch"></right-toolbar>
    </el-row>

    <!-- 数据区域 -->
    <el-table :data="dataList" ref="table" border>
      <el-table-column prop="id" label="id" width="60" sortable> </el-table-column>
      <el-table-column prop="saveStore" label="存储位置" :show-overflow-tooltip="true"> </el-table-column>
      <!-- 显示图片 -->
      <el-table-column prop="photo" label="图片预览" width="110">
        <template slot-scope="scope">
          <el-popover placement="right" trigger="hover">
            <!-- click显示的大图 -->
            <img :src="scope.row.photo" />
            <img slot="reference" :src="scope.row.photo" width="100" height="50">
          </el-popover>
        </template>
      </el-table-column>
      <el-table-column prop="savePath" label="文件仓库" :show-overflow-tooltip="true" />
      <el-table-column prop="fileName" label="文件名" :show-overflow-tooltip="true" />
      <el-table-column prop="fileExt" label="文件扩展名" />
      <el-table-column prop="fileSize" label="文件大小" />
      <el-table-column prop="addtime" label="创建时间"> </el-table-column>
      <el-table-column label="操作" align="center" width="200">
        <template slot-scope="scope">
          <el-button size="mini" type="text" icon="el-icon-view" @click="handleView(scope.row)">详情</el-button>
          <el-button size="mini" type="text" icon="el-icon-edit" @click="handleUpdate(scope.row)">编辑</el-button>
          <el-button size="mini" type="text" icon="el-icon-delete" @click="handleDelete(scope.row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>
    <pagination :total="total" :page.sync="queryParams.pageNum" :limit.sync="queryParams.pageSize" @pagination="getList" />

  </div>
</template>
<script>
// import { getXXXX} from '@/api/xxxx.js'

export default {
  name: "file",
  data() {
    return {
      // 遮罩层
      loading: true,
      // 显示搜索条件
      showSearch: true,
      // 查询参数
      queryParams: {},
      // 弹出层标题
      title: "",
      // 是否显示弹出层
      open: false,
      // 表单参数
      form: {},
      // xxx下拉框
      options: [
        {
          dictValue: 1,
          dictLabel: "阿里云",
        },
        {
          dictValue: 2,
          dictLabel: "腾讯云",
        },
        {
          dictValue: 3,
          dictLabel: "七牛云",
        },
        {
          dictValue: 4,
          dictLabel: "本地",
        },
      ],
      uploadUrl: process.env.VUE_APP_BASE_API + "/upload/SaveFile?token=zr",
      // 数据列表
      dataList: [
        {
          id: 1,
          photo:
            "https://ss1.baidu.com/-4o3dSag_xI4khGko9WTAnF6hhy/zhidao/pic/item/d788d43f8794a4c2b124d0000df41bd5ad6e3991.jpg",
          name: "你好",
          userId: 1000001,
          sortId: 1,
          address: "浙江省杭州市西湖区",
          content: "我是一个超长超长的文字啊",
          addtime: "2021-8-7 23:00:00",
        },
      ],
      // 总记录数
      total: 0,
      // 提交按钮是否显示
      btnSubmitVisible: true,
      // 表单校验
      rules: {
        name: [{ required: true, message: "名称不能为空", trigger: "blur" }],
        userId: [{ required: true, message: "id不能为空", trigger: "blur" }],
      },
    };
  },
  mounted() {
    this.getList();
  },
  methods: {
    // 查询数据
    getList() {
      console.log(JSON.stringify(this.queryParams));
      console.log(process.env.VUE_APP_BASE_API);
      //TODO 请求数据
    },
    // 取消按钮
    cancel() {
      this.open = false;
      this.reset();
    },
    // 重置数据表单
    reset() {
      this.btnSubmitVisible = true;
      this.form = {
        Id: undefined,
        // TODO 其他列字段
      };
      this.resetForm("form");
    },
    /** 重置查询操作 */
    resetQuery() {
      this.resetForm("queryForm");
    },
    /** 搜索按钮操作 */
    handleQuery() {
      this.getList();
    },
    handleChange(file, fileList) {
      // this.fileList = fileList.slice(-3);
    },
    /** 删除按钮操作 */
    handleDelete(row) {
      this.$confirm("是否确认删除吗?", "警告", {
        confirmButtonText: "确定",
        cancelButtonText: "取消",
        type: "warning",
      })
        .then(function () {
          // TODO 网络请求删除操作
          //return delMenu(row.menuId);
        })
        .then(() => {
          // this.getList();
          this.msgSuccess("删除成功");
        });

      //TODO 业务代码
    },
    /** 修改按钮操作 */
    handleUpdate(row) {
      this.reset();
      this.open = true;
      this.title = "编辑";
      //TODO 业务代码
      console.log(JSON.stringify(row));

      // TODO 给表单赋值
      this.form = {
        content: row.content,
        userId: row.userId,
        name: row.name,
        sortId: row.sortId,
      };
    },
    /** 提交按钮 */
    submitForm: function () {
      this.$refs["form"].validate((valid) => {
        if (valid) {
          console.log(JSON.stringify(this.form));
          // TODO 记得改成表的主键
          if (this.form.Id != undefined) {
            //TODO 业务代码
          } else {
            //TODO 业务代码
          }
        }
      });
    },
    // 详情
    handleView(row) {
      this.open = true;
      this.title = "详情";
      // TODO 给表单赋值
      this.form = {
        content: row.content,
        userId: row.userId,
        name: row.name,
        sortId: row.sortId,
      };
    },
    handleImport() {},
    handleExport() {},
  },
};
</script>
