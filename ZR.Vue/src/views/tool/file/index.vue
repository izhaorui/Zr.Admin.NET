<template>
  <div class="app-container">
    <!-- :model属性用于表单验证使用 比如下面的el-form-item 的 prop属性用于对表单值进行验证操作 -->
    <el-form :model="queryParams" label-position="left" inline ref="queryForm" :label-width="labelWidth" v-show="showSearch" @submit.native.prevent>
      <el-form-item label="文件id" prop="fileId">
        <el-input v-model="queryParams.fileId" placeholder="请输入文件id" clearable size="small" />
      </el-form-item>
      <el-form-item label="上传时间">
        <el-date-picker v-model="dateRangeCreate_time" size="small" value-format="yyyy-MM-dd" type="daterange" range-separator="-"
          start-placeholder="开始日期" end-placeholder="结束日期" placeholder="请选择上传时间"></el-date-picker>
      </el-form-item>
      <el-form-item label="存储类型" prop="storeType">
        <el-select v-model="queryParams.storeType" placeholder="请选择存储类型" size="small" clearable="">
          <el-option v-for="item in storeTypeOptions" :key="item.dictValue" :label="item.dictLabel" :value="item.dictValue"></el-option>
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button type="primary" icon="el-icon-search" size="mini" @click="handleQuery">搜索</el-button>
        <el-button icon="el-icon-refresh" size="mini" @click="resetQuery">重置</el-button>
      </el-form-item>
    </el-form>
    <!-- 工具区域 -->
    <el-row :gutter="10" class="mb8">
      <el-col :span="1.5">
        <el-button type="primary" v-hasPermi="['tool:file:add']" plain icon="el-icon-upload" size="mini" @click="handleAdd">上传文件</el-button>
      </el-col>
      <el-col :span="1.5">
        <el-button type="danger" :disabled="multiple" v-hasPermi="['tool:file:delete']" plain icon="el-icon-delete" size="mini" @click="handleDelete">
          删除</el-button>
      </el-col>
      <right-toolbar :showSearch.sync="showSearch" @queryTable="getList"></right-toolbar>
    </el-row>

    <!-- 数据区域 -->
    <el-table :data="dataList" v-loading="loading" ref="table" border highlight-current-row @selection-change="handleSelectionChange">
      <el-table-column type="selection" width="50" align="center" />
      <el-table-column prop="id" label="文件id" align="center" width="80" />
      <el-table-column prop="fileName" label="文件名" align="center">
        <template slot-scope="scope">
          <el-popover :content="scope.row.fileUrl" placement="top-start" title="路径" trigger="hover">
            <a slot="reference" :href="scope.row.accessUrl" class="el-link--primary"
              style="word-break:keep-all;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;color: #1890ff;font-size: 13px;" target="_blank">
              {{ scope.row.fileName }}
            </a>
          </el-popover>
        </template>
      </el-table-column>
      <el-table-column prop="accessUrl" align="center" label="预览图" width="100">
        <template slot-scope="{row}">
          <el-image :src="row.accessUrl" :preview-src-list="[row.accessUrl]" fit="contain" lazy class="el-avatar">
            <div slot="error">
              <i class="el-icon-document" />
            </div>
          </el-image>
        </template>
      </el-table-column>
      <el-table-column prop="fileSize" label="文件大小" align="center" :show-overflow-tooltip="true" />
      <el-table-column prop="fileExt" label="扩展名" align="center" :show-overflow-tooltip="true" width="80px" />
      <el-table-column prop="storeType" label="存储类型" align="center" :formatter="storeTypeFormat" />
      <el-table-column prop="create_by" label="操作人" align="center" />
      <el-table-column prop="create_time" label="创建日期" align="center" />
      <el-table-column label="操作" align="center" width="200">
        <template slot-scope="scope">
          <el-button type="text" icon="el-icon-view" @click="handleView(scope.row)">查看</el-button>
          <el-button v-hasPermi="['tool:file:delete']" type="text" icon="el-icon-delete" @click="handleDelete(scope.row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>
    <pagination class="mt10" background :total="total" :page.sync="queryParams.pageNum" :limit.sync="queryParams.pageSize" @pagination="getList" />

    <!-- 添加或修改文件存储对话框 -->
    <el-dialog :title="title" :lock-scroll="false" :visible.sync="open" width="400px">
      <el-form ref="form" :model="form" :rules="rules" label-width="135px" label-position="left">
        <el-row>
          <el-col :lg="24">
            <el-form-item label="存储类型" prop="storeType">
              <el-select v-model="form.storeType" placeholder="请选择存储类型" @change="handleSelectStore">
                <el-option v-for="item in storeTypeOptions" :key="item.dictValue" :label="item.dictLabel" :value="parseInt(item.dictValue)">
                </el-option>
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :lg="24">
            <el-form-item label="存储文件夹前缀" prop="storePath">
              <span slot="label">
                存储文件夹前缀
                <el-tooltip content="比如存储到'/uploads' '如果不填写默认按时间存储eg：/2021/12/16(固定段)'" placement="top">
                  <i class="el-icon-question"></i>
                </el-tooltip>
              </span>
              <el-input v-model="form.storePath" placeholder="请输入" clearable=""/>
            </el-form-item>
          </el-col>
          <el-col :lg="24">
            <el-form-item label="自定文件名" prop="fileName">
              <el-input v-model="form.fileName" placeholder="请输入文件名" clearable=""/>
            </el-form-item>
          </el-col>
          <el-col :lg="24">
            <el-form-item label="上传文件" prop="accessUrl">
              <UploadFile v-model="form.accessUrl" :uploadUrl="uploadUrl" :fileType="[]" :limit="1" :fileSize="15"
                :data="{ 'fileDir' :  form.storePath, 'fileName': form.fileName}" column="accessUrl" @input="handleUploadSuccess" />
            </el-form-item>
          </el-col>

        </el-row>
      </el-form>
      <div slot="footer" class="dialog-footer">
        <el-button @click="cancel">取 消</el-button>
      </div>
    </el-dialog>

    <!-- 添加或修改文件存储对话框 -->
    <el-dialog title="查看" :lock-scroll="false" :visible.sync="openView">
      <el-form ref="form" :model="formView" :rules="rules" :label-width="formLabelWidth">
        <el-row>
          <el-col :lg="12">
            <el-form-item label="文件id">{{formView.id}}</el-form-item>
          </el-col>
          <el-col :lg="12">
            <el-form-item label="扩展名">{{formView.fileExt}}</el-form-item>
          </el-col>
          <el-col :lg="12">
            <el-form-item label="源文件名">{{formView.realName}}</el-form-item>
          </el-col>
          <el-col :lg="12">
            <el-form-item label="文件名">{{formView.fileName}}</el-form-item>
          </el-col>
          <el-col :lg="12">
            <el-form-item label="仓库位置">{{formView.storePath}}</el-form-item>
          </el-col>
          <el-col :lg="12">
            <el-form-item label="文件大小">{{formView.fileSize}}</el-form-item>
          </el-col>
          <el-col :lg="12">
            <el-form-item label="创建人">{{formView.create_by}}</el-form-item>
          </el-col>
          <el-col :lg="24">
            <el-form-item label="存储路径">{{formView.fileUrl}}</el-form-item>
          </el-col>
          <el-col :lg="24">
            <el-form-item label="访问路径">{{formView.accessUrl}}</el-form-item>
          </el-col>
        </el-row>
      </el-form>
    </el-dialog>
  </div>
</template>
<script>
import { listSysfile, delSysfile, getSysfile } from "@/api/tool/file.js";

export default {
  name: "sysfile",
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
        fileId: undefined,
      },
      // 弹出层标题
      title: "",
      // 是否显示弹出层
      open: false,
      openView: false,
      // 表单参数
      form: {},
      formView: {},
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
      fileType: [],
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
  },
  methods: {
    // 查询数据
    getList() {
      this.addDateRange(this.queryParams, this.dateRangeCreate_time, 'Create_time');
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
        fileName: "",
        fileUrl: "",
        storePath: "uploads",
        fileSize: 0,
        fileExt: "",
        storeType: 1,
        accessUrl: "",
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
      this.form.storeType = this.queryParams.storeType;
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
    /**查看按钮操作 */
    handleView(row) {
      const id = row.id || this.ids;
      getSysfile(id).then((res) => {
        const { code, data } = res;
        if (code == 200) {
          this.openView = true;
          this.formView = data;
        }
      });
    },
    //上传成功方法
    handleUploadSuccess(columnName, filelist, data) {
      this.form[columnName] = filelist;
      this.queryParams.fileId = data.fileId;
      this.open = false;
      this.getList();
    },
    // 存储类型字典翻译
    storeTypeFormat(row, column) {
      return this.selectDictLabel(this.storeTypeOptions, row.storeType);
    },
    handleSelectStore(val) {
      this.queryParams.storeType = val;
      if (val == 1) {
        this.uploadUrl = "/common/uploadFile";
      } else if (val == 2) {
        this.uploadUrl = "/common/UploadFileAliyun";
      }
    },
  },
};
</script>
<style scoped>
.el-avatar {
  display: inline-block;
  text-align: center;
  background: #ccc;
  color: #fff;
  white-space: nowrap;
  position: relative;
  overflow: hidden;
  vertical-align: middle;
  width: 32px;
  height: 32px;
  line-height: 32px;
  border-radius: 16px;
}
</style>