<template>
  <div class="app-container">
    <!-- :model属性用于表单验证使用 比如下面的el-form-item 的 prop属性用于对表单值进行验证操作 -->
    <el-form :model="queryParams" label-position="left" inline ref="queryForm" :label-width="labelWidth" v-show="showSearch" @submit.native.prevent>
      <el-form-item label="上传时间">
        <el-date-picker v-model="dateRangeCreate_time" size="small" value-format="yyyy-MM-dd" type="daterange" range-separator="-"
          start-placeholder="开始日期" end-placeholder="结束日期" placeholder="请选择上传时间"></el-date-picker>
      </el-form-item>
      <el-form-item label="存储类型" prop="storeType">
        <el-select v-model="queryParams.storeType" placeholder="请选择存储类型" size="small" clearable="">
          <el-option v-for="item in storeTypeOptions" :key="item.dictValue" :label="item.dictLabel" :value="item.dictValue"></el-option>
        </el-select>
      </el-form-item>

      <el-row class="mb8" style="text-align:center">
        <el-button type="primary" icon="el-icon-search" size="mini" @click="handleQuery">搜索</el-button>
        <el-button icon="el-icon-refresh" size="mini" @click="resetQuery">重置</el-button>
      </el-row>
    </el-form>
    <!-- 工具区域 -->
    <el-row :gutter="10" class="mb8">
      <el-col :span="1.5">
        <el-button type="primary" v-hasPermi="['System:sysfile:add']" plain icon="el-icon-upload" size="mini" @click="handleAdd">上传文件</el-button>
      </el-col>
      <!-- <el-col :span="1.5">
        <el-button type="success" :disabled="single" v-hasPermi="['System:sysfile:update']" plain icon="el-icon-edit" size="mini" @click="handleUpdate">修改</el-button>
      </el-col> -->
      <el-col :span="1.5">
        <el-button type="danger" :disabled="multiple" v-hasPermi="['System:sysfile:delete']" plain icon="el-icon-delete" size="mini"
          @click="handleDelete">删除</el-button>
      </el-col>
      <right-toolbar :showSearch.sync="showSearch" @queryTable="getList"></right-toolbar>
    </el-row>

    <!-- 数据区域 -->
    <el-table :data="dataList" v-loading="loading" ref="table" border highlight-current-row @selection-change="handleSelectionChange">
      <el-table-column type="selection" width="50" align="center" />
      <el-table-column prop="id" label="文件id" align="center" width="80" />
      <el-table-column prop="fileName" label="文件名" align="center" :show-overflow-tooltip="true" />
      <el-table-column prop="storePath" label="仓库位置" align="center" :show-overflow-tooltip="true" />
      <el-table-column prop="fileSize" label="文件大小" align="center" :show-overflow-tooltip="true" />
      <el-table-column prop="fileExt" label="扩展名" align="center" :show-overflow-tooltip="true" width="80px" />
      <el-table-column prop="storeType" label="存储类型" align="center" :formatter="storeTypeFormat" />
			<el-table-column prop="create_time" label="存储时间" align="center"/>
      <el-table-column prop="accessUrl" label="访问路径" align="center" :show-overflow-tooltip="true">
      </el-table-column>
      <el-table-column label="操作" align="center" width="200">
        <template slot-scope="scope">
          <!-- <el-button v-hasPermi="['System:sysfile:update']" type="text" icon="el-icon-edit" @click="handleUpdate(scope.row)">编辑</el-button> -->
          <el-button v-hasPermi="['System:sysfile:delete']" type="text" icon="el-icon-delete" @click="handleDelete(scope.row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>
    <pagination class="mt10" background :total="total" :page.sync="queryParams.pageNum" :limit.sync="queryParams.pageSize" @pagination="getList" />

    <!-- 添加或修改文件存储对话框 -->
    <el-dialog :title="title" :lock-scroll="false" :visible.sync="open">
      <el-form ref="form" :model="form" :rules="rules" label-width="135px">
        <el-row>
          <!-- <el-col :lg="12">
            <el-form-item label="自定文件名" prop="fileName">
              <el-input v-model="form.fileName" placeholder="请输入文件名" />
            </el-form-item>
          </el-col> -->
          <el-col :lg="12">
            <el-form-item label="存储类型" prop="storeType">
              <el-select v-model="form.storeType" placeholder="请选择存储类型" @change="handleSelectStore">
                <el-option v-for="item in storeTypeOptions" :key="item.dictValue" :label="item.dictLabel" :value="parseInt(item.dictValue)">
                </el-option>
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :lg="12">
            <el-form-item label="存储文件夹前缀" prop="storePath">
              <span slot="label">
                <el-tooltip content="比如存储到'/uploads' '如果不填写默认按时间存储eg：/2021/12/16(固定段)'" placement="top">
                  <i class="el-icon-question"></i>
                </el-tooltip>
                存储文件夹前缀
              </span>
              <el-input v-model="form.storePath" placeholder="请输入" />
            </el-form-item>
          </el-col>
          <el-col :lg="24">
            <el-form-item label="上传文件" prop="accessUrl">
              <UploadFile v-model="form.accessUrl" :uploadUrl="uploadUrl" :fileType="fileType" :data="{ 'fileDir' :  form.storePath}"
                column="accessUrl" @input="handleUploadSuccess" />
            </el-form-item>
          </el-col>

        </el-row>
      </el-form>
      <div slot="footer" class="dialog-footer" v-if="btnSubmitVisible">
        <el-button type="primary" @click="submitForm">确 定</el-button>
        <el-button @click="cancel">取 消</el-button>
      </div>
    </el-dialog>

  </div>
</template>
<script>
import {
  listSysfile,
  addSysfile,
  delSysfile,
  updateSysfile,
  getSysfile,
  exportSysfile,
} from "@/api/tool/file.js";

export default {
  name: "Sysfile",
  data() {
    return {
      labelWidth: "100px",
      formLabelWidth: "100px",
      // 选中id数组
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
      queryParams: {
        pageNum: 1,
        pageSize: 20,
        storeType: 1,
      },
      // 弹出层标题
      title: "",
      // 是否显示弹出层
      open: false,
      // 表单参数
      form: {
      },
      columns: [
        { index: 0, key: "id", label: `自增id`, checked: true },
        { index: 1, key: "fileName", label: `文件名`, checked: true },
        { index: 2, key: "fileUrl", label: `文件存储地址`, checked: true },
        { index: 3, key: "storePath", label: `仓库位置`, checked: true },
        { index: 4, key: "fileSize", label: `文件大小`, checked: true },
        { index: 5, key: "fileExt", label: `文件扩展名`, checked: true },
        { index: 6, key: "create_by", label: `创建人`, checked: true },
        { index: 7, key: "create_time", label: `上传时间`, checked: true },
        { index: 8, key: "storeType", label: `存储类型`, checked: true },
        { index: 9, key: "accessUrl", label: `访问路径`, checked: false },
      ],
      //上传时间时间范围
      dateRangeCreate_time: [],
      // 存储类型选项列表
      storeTypeOptions: [
        { dictLabel: "本地存储", dictValue: 1 },
        { dictLabel: "阿里云存储", dictValue: 2 },
      ],
      // 上传文件地址
      uploadUrl: "/common/uploadFile",
      fileType: [
        "doc",
        "xls",
        "ppt",
        "txt",
        "pdf",
        "svga",
        "json",
        "jpg",
        "jpeg",
        "png",
      ],
      // 数据列表
      dataList: [],
      // 总记录数
      total: 0,
      // 提交按钮是否显示
      btnSubmitVisible: true,
      // 表单校验
      rules: {
        accessUrl: [
          {
            required: true,
            message: "上传文件不能为空",
            trigger: "blur",
          },
        ],
        storeType: [
          {
            required: true,
            message: "存储类型不能为空",
            trigger: "blur",
          },
        ],
      },
    };
  },
  created() {
    // 列表数据查询
    this.getList();

    var dictParams = [];
  },
  methods: {
    // 查询数据
    getList() {
      this.queryParams["beginCreate_time"] = this.addDateRange2(
        this.dateRangeCreate_time,
        0
      );
      this.queryParams["endCreate_time"] = this.addDateRange2(
        this.dateRangeCreate_time,
        1
      );
      this.loading = true;
      listSysfile(this.queryParams).then((res) => {
        if (res.code == 200) {
          this.dataList = res.data.result;
          this.total = res.data.totalNum;
          this.loading = false;
        }
      });
    },
    // 取消按钮
    cancel() {
      this.open = false;
      this.reset();
    },
    // 重置数据表单
    reset() {
      this.form = {
        fileName: undefined,
        fileUrl: undefined,
        storePath: "",
        fileSize: undefined,
        fileExt: undefined,
        storeType: 1,
        accessUrl: undefined,
      };
      this.resetForm("form");
    },
    /** 重置查询操作 */
    resetQuery() {
      this.timeRange = [];
      //上传时间时间范围
      this.dateRangeCreate_time = [];
      this.resetForm("queryForm");
      this.handleQuery();
    },
    // 多选框选中数据
    handleSelectionChange(selection) {
      this.ids = selection.map((item) => item.id);
      this.single = selection.length != 1;
      this.multiple = !selection.length;
    },
    /** 搜索按钮操作 */
    handleQuery() {
      this.queryParams.pageNum = 1;
      this.getList();
    },
    /** 新增按钮操作 */
    handleAdd() {
      this.reset();
      this.open = true;
      this.title = "上传文件";
    },
    /** 删除按钮操作 */
    handleDelete(row) {
      const Ids = row.id || this.ids;

      this.$confirm('是否确认删除参数编号为"' + Ids + '"的数据项？')
        .then(function () {
          return delSysfile(Ids);
        })
        .then(() => {
          this.handleQuery();
          this.msgSuccess("删除成功");
        })
        .catch(() => {});
    },
    /** 修改按钮操作 */
    handleUpdate(row) {
      this.reset();
      const id = row.id || this.ids;
      getSysfile(id).then((res) => {
        const { code, data } = res;
        if (code == 200) {
          this.open = true;
          this.title = "修改数据";

          this.form = {
            ...data,
          };
        }
      });
    },
    //图片上传成功方法
    handleUploadSuccess(columnName, filelist) {
      this.form[columnName] = filelist;
    },
    // 存储类型字典翻译
    storeTypeFormat(row, column) {
      return this.selectDictLabel(this.storeTypeOptions, row.storeType);
    },
    handleSelectStore(val) {
      if (val == 1) {
        this.uploadUrl = "/common/uploadFile";
      } else if (val == 2) {
        thiis.uploadUrl = "/common/UploadFileAliyun";
      }
    },
    /** 提交按钮 */
    submitForm: function () {
      this.$refs["form"].validate((valid) => {
        if (valid) {
          console.log(JSON.stringify(this.form));

          if (this.form.id != undefined && this.title === "修改数据") {
            updateSysfile(this.form)
              .then((res) => {
                this.msgSuccess("修改成功");
                this.open = false;
                this.getList();
              })
              .catch((err) => {
                //TODO 错误逻辑
              });
          } else {
            this.open = false;
            this.getList();
          }
        }
      });
    },
    /** 导出按钮操作 */
    // handleExport() {
    //   const queryParams = this.queryParams;
    //   this.$confirm("是否确认导出所有文件存储数据项?", "警告", {
    //     confirmButtonText: "确定",
    //     cancelButtonText: "取消",
    //     type: "warning",
    //   })
    //     .then(function () {
    //       return exportSysfile(queryParams);
    //     })
    //     .then((response) => {
    //       this.download(response.data.path);
    //     });
    // },
  },
};
</script>