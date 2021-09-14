<template>
  <div class="app-container">
    <!-- :model属性用于表单验证使用 比如下面的el-form-item 的 prop属性用于对表单值进行验证操作 -->
    <el-form :model="queryParams" label-position="left" inline ref="queryForm" :label-width="labelWidth" v-show="showSearch" @submit.native.prevent>
      <el-form-item label="文本文字">
        <el-input v-model="queryParams.xxx" placeholder="" />
      </el-form-item>
      <el-form-item label="数字">
        <el-input v-model.number="queryParams.xxx" placeholder="" />
      </el-form-item>

      <el-form-item label="下拉框">
        <el-select v-model="queryParams.xxx" placeholder="">
          <el-option v-for="dict in statusOptions" :key="dict.dictValue" :label="dict.dictLabel" :value="dict.dictValue" />
        </el-select>
      </el-form-item>

      <el-form-item label="时间范围">
        <el-date-picker size="small" style="width: 240px" v-model="timeRange" value-format="yyyy-MM-dd" type="daterange" range-separator="-" start-placeholder="开始日期" end-placeholder="结束日期">
        </el-date-picker>
      </el-form-item>

      <el-row class="mb8" style="text-align:center">
        <el-button type="primary" icon="el-icon-search" size="mini" @click="handleQuery">搜索</el-button>
        <el-button icon="el-icon-refresh" size="mini" @click="resetQuery">重置</el-button>
      </el-row>
    </el-form>

    <!-- 工具区域 -->
    <el-row :gutter="10" class="mb8">
      <el-col :span="1.5">
        <el-button type="primary" v-hasPermi="['gendemo:add']" plain icon="el-icon-plus" size="mini" @click="handleAdd">新增</el-button>
      </el-col>
      <el-col :span="1.5">
        <el-button type="success" :disabled="single" v-hasPermi="['gendemo:update']" plain icon="el-icon-edit" size="mini" @click="handleUpdate">修改</el-button>
      </el-col>
      <el-col :span="1.5">
        <el-button type="danger" v-hasPermi="['gendemo:delete']" plain icon="el-icon-delete" size="mini" @click="handleDelete">删除</el-button>
      </el-col>
      <right-toolbar :showSearch.sync="showSearch" @queryTable="getList"></right-toolbar>
    </el-row>

    <!-- 数据区域 -->
    <el-table :data="dataList" ref="table" border @selection-change="handleSelectionChange">
      <el-table-column type="selection" width="50" />
      <el-table-column prop="id" label="自增id" align="center" width="100" />
      <el-table-column prop="name" label="名称" align="center" width="100" :show-overflow-tooltip="true" />
      <el-table-column prop="icon" label="图片">
        <template slot-scope="scope">
          <el-image class="table-td-thumb" :src="scope.row.icon" :preview-src-list="[scope.row.icon]"></el-image>
        </template>
      </el-table-column>
      <el-table-column prop="showStatus" label="显示状态" align="center" width="100" />
      <el-table-column prop="addTime" label="添加时间" align="center" width="100" />

      <el-table-column label="操作" align="center" width="200">
        <template slot-scope="scope">
          <el-button size="mini" v-hasPermi="['gendemo:update']" type="text" icon="el-icon-edit" @click="handleUpdate(scope.row)">编辑</el-button>
          <el-popconfirm title="确定删除吗？" @onConfirm="handleDelete(scope.row)" style="margin-left:10px">
            <el-button slot="reference" v-hasPermi="['gendemo:delete']" size="mini" type="text" icon="el-icon-delete">删除</el-button>
          </el-popconfirm>
        </template>
      </el-table-column>
    </el-table>
    <el-pagination class="mt10" background :total="total" :current-page.sync="queryParams.pageNum" layout="total, sizes, prev, pager, next, jumper" :page-size="queryParams.pageSize" :page-sizes="[20, 30, 50, 100]" @size-change="handleSizeChange"
      @current-change="getList" />

    <!-- 添加或修改菜单对话框 -->
    <el-dialog :title="title" :lock-scroll="false" :visible.sync="open">
      <el-form ref="form" :model="form" :rules="rules" :label-width="formLabelWidth">
        <el-form-item label="自增id" :label-width="labelWidth" prop="id">
          <el-input v-model="form.id" placeholder="" :disabled="true" />
        </el-form-item>
        <el-form-item label="名称" :label-width="labelWidth" prop="name">
          <el-input v-model="form.name" placeholder="请输入名称" />
        </el-form-item>
        <el-form-item label="图片" :label-width="labelWidth" prop="icon">
          <el-upload class="avatar-uploader" name="file" action="/api/upload/saveFile/" :show-file-list="false" :on-success="handleUploadiconSuccess" :before-upload="beforeFileUpload">
            <img v-if="form.icon" :src="form.icon" class="icon">
            <i v-else class="el-icon-plus uploader-icon"></i>
          </el-upload>
          <el-input v-model="form.icon" placeholder="请上传文件或手动输入文件地址"></el-input>
        </el-form-item>
        <el-form-item label="显示状态" :label-width="labelWidth" prop="showStatus">
          <el-input v-model="form.showStatus" placeholder="请输入显示状态" />
        </el-form-item>
        <el-form-item label="添加时间" :label-width="labelWidth" prop="addTime">
          <el-date-picker v-model="form.addTime" type="datetime" placeholder="选择日期时间" default-time="12:00:00"> </el-date-picker>
        </el-form-item>

      </el-form>
      <div slot="footer" class="dialog-footer" v-if="btnSubmitVisible">
        <el-button @click="cancel">取 消</el-button>
        <el-button type="primary" @click="submitForm">确 定</el-button>
      </div>
    </el-dialog>

  </div>
</template>
<script>
import {
  listGendemo,
  addGendemo,
  delGendemo,
  updateGendemo,
  getGendemo,
} from "@/api/gendemo.js";

export default {
  name: "Gendemo",
  data() {
    return {
      labelWidth: "100px",
      formLabelWidth: "100px",
      // 选中数组
      ids: [],
      // 非单个禁用
      single: true,
      // 非多个禁用
      multiple: true,
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
      // 时间范围数组
      timeRange: [],
      // xxx下拉框
      statusOptions: [],
      // 数据列表
      dataList: [],
      // 总记录数
      total: 0,
      // 提交按钮是否显示
      btnSubmitVisible: true,
      // 表单校验
      rules: {
        name: [{ required: true, message: "请输入名称", trigger: "blur" }],
        showStatus: [
          { required: true, message: "请输入显示状态", trigger: "blur" },
        ],
      },
    };
  },
  mounted() {
    // 列表数据查询
    this.getList();
    // 下拉框绑定
    // this.getDicts("sys_normal_disable").then((response) => {
    //   this.statusOptions = response.data;
    // });
  },
  methods: {
    // 查询数据
    getList() {
      console.log(JSON.stringify(this.queryParams));
      listGendemo(this.addDateRange(this.queryParams, this.timeRange)).then(
        (res) => {
          if (res.code == 200) {
            this.dataList = res.data.result;
            this.total = res.data.totalCount;
          }
        }
      );
    },
    // 取消按钮
    cancel() {
      this.open = false;
      this.reset();
    },
    // 重置数据表单
    reset() {
      this.form = {
        id: undefined,
        name: undefined,
        icon: undefined,
        showStatus: undefined,
        addTime: undefined,

        //需个性化处理内容
      };
      this.resetForm("form");
    },
    /** 重置查询操作 */
    resetQuery() {
      this.timeRange = [];
      this.resetForm("queryForm");
      this.queryParams = {
        pageNum: 1,
        pageSize: 20,
        //TODO 重置字段
      };
    },
    // 多选框选中数据
    handleSelectionChange(selection) {
      this.ids = selection.map((item) => item.id);
      this.single = selection.length != 1;
      this.multiple = !selection.length;
    },
    /** 选择每页显示数量*/
    handleSizeChange(val) {
      this.queryParams.pageSize = val;
      this.queryParams.pageNum = 1;
      this.handleQuery();
    },
    /** 搜索按钮操作 */
    handleQuery() {
      this.getList();
    },
    /** 新增按钮操作 */
    handleAdd() {
      this.reset();
      this.open = true;
      this.title = "添加";
    },
    /** 删除按钮操作 */
    handleDelete(row) {
      delGendemo(row.id).then((res) => {
        this.msgSuccess("删除成功");
        this.handleQuery();
      });
    },
    /** 修改按钮操作 */
    handleUpdate(row) {
      this.reset();
      const id = row.id || this.ids;
      getGendemo(id).then((res) => {
        if (res.code == 200) {
          this.form = res.data;
          this.open = true;
          this.title = "修改数据";
        }
      });
    },
    beforeFileUpload(file) {},
    //文件上传成功方法
    handleUploadiconSuccess(res, file) {
      this.form.icon = URL.createObjectURL(file.raw);
      // this.$refs.upload.clearFiles();
    },

    /** 提交按钮 */
    submitForm: function () {
      this.$refs["form"].validate((valid) => {
        if (valid) {
          console.log(JSON.stringify(this.form));

          if (this.form.id != undefined || this.title === "修改数据") {
            updateGendemo(this.form).then((res) => {
              this.msgSuccess("修改成功");
              this.open = false;
              this.getList();
            });
          } else {
            addGendemo(this.form).then((res) => {
              this.msgSuccess("新增成功");
              this.open = false;
              this.getList();
            });
          }
        }
      });
    },
  },
};
</script>
<style scoped>
.table-td-thumb {
  width: 80px;
  height: 100%;
}
</style>
