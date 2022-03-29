<template>
  <div class="app-container">
    <!-- :model属性用于表单验证使用 比如下面的el-form-item 的 prop属性用于对表单值进行验证操作 -->
    <el-form :model="queryParams" label-position="left" inline ref="queryForm" :label-width="labelWidth" v-show="showSearch" @submit.native.prevent>
      <el-form-item label="" prop="fileId">
        <el-input v-model="queryParams.fileId" placeholder="请输入文件id" clearable size="small" />
      </el-form-item>
      <el-form-item label="">
        <el-date-picker v-model="dateRangeAddTime" size="small" value-format="yyyy-MM-dd" type="daterange" range-separator="-"
          start-placeholder="开始日期" end-placeholder="结束日期" placeholder="请选择上传时间"></el-date-picker>
      </el-form-item>
      <el-form-item label="" prop="storeType">
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
      <el-table-column prop="id" label="文件id" align="center" width="150" />
      <el-table-column prop="fileName" label="文件名" align="center" width="175" :show-overflow-tooltip="true">
        <template slot-scope="scope">
          <el-link type="primary" :href="scope.row.accessUrl" target="_blank">{{ scope.row.fileName }}</el-link>
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
      <el-table-column prop="create_time" label="创建日期" align="center" width="150" />
      <el-table-column label="操作" align="center" width="200">
        <template slot-scope="scope">
          <el-button type="text" icon="el-icon-view" @click="handleView(scope.row)">查看</el-button>
          <el-button class="copy-btn-main" icon="el-icon-document-copy" type="text" v-clipboard:copy="scope.row.accessUrl"
            v-clipboard:success="clipboardSuccess">
            复制
          </el-button>
          <el-button v-hasPermi="['tool:file:delete']" type="text" icon="el-icon-delete" @click="handleDelete(scope.row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>
    <pagination class="mt10" background :total="total" :page.sync="queryParams.pageNum" :limit.sync="queryParams.pageSize" @pagination="getList" />

    <!-- 添加或修改文件存储对话框 -->
    <el-dialog :title="title" :lock-scroll="false" :visible.sync="open" width="400px">
      <el-form ref="form" :model="form" :rules="rules" label-width="90px" label-position="left">
        <el-row>
          <el-col :lg="24">
            <el-form-item label="存储类型" prop="storeType">
              <el-radio-group v-model="form.storeType" placeholder="请选择存储类型">
                <el-radio v-for="item in storeTypeOptions" :key="item.dictValue" :label="parseInt(item.dictValue)">
                  {{item.dictLabel}}
                </el-radio>
              </el-radio-group>
            </el-form-item>
          </el-col>
          <!-- <el-col :lg="24">
            <el-form-item label="按时间存储" prop="timeStore">
              <el-radio-group v-model="form.storeType" placeholder="是否按时间(yyyyMMdd)存储">
                <el-radio :key="1" :label="1">是 </el-radio>
                <el-radio :key="0" :label="0">否 </el-radio>
              </el-radio-group>
            </el-form-item>
          </el-col> -->
          <el-col :lg="24">
            <el-form-item label="存储文件夹" prop="storePath">
              <el-input v-model="form.storePath" placeholder="请输入存储文件夹" clearable="" auto-complete="" />
            </el-form-item>
          </el-col>
          <el-col :lg="24">
            <el-form-item label="自定文件名" prop="fileName">
              <el-input v-model="form.fileName" placeholder="请输入文件名" clearable="" />
            </el-form-item>
          </el-col>
          <el-col :lg="24">
            <el-form-item prop="accessUrl">
              <UploadFile ref="upload" v-model="form.accessUrl" :fileType="[]" :limit="5" :fileSize="15" :drag="true"
                :data="{ 'fileDir' :  form.storePath, 'fileName': form.fileName, 'storeType': form.storeType}" :autoUpload="false" column="accessUrl"
                @input="handleUploadSuccess" />
            </el-form-item>
          </el-col>
        </el-row>
      </el-form>
      <div slot="footer" class="dialog-footer">
        <el-button type="text" @click="cancel">取 消</el-button>
        <el-button type="primary" @click="submitUpload">确定上传</el-button>
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
            <el-form-item label="源文件名">{{formView.realName}}</el-form-item>
          </el-col>
          <el-col :lg="12">
            <el-form-item label="文件类型">
              <el-tag>{{formView.fileType}}</el-tag>
            </el-form-item>
          </el-col>
          <el-col :lg="12">
            <el-form-item label="扩展名">
              <el-tag>{{formView.fileExt}}</el-tag>
            </el-form-item>
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
          <el-col :lg="24" v-if="['.png','.jpg', '.jpeg'].includes(formView.fileExt)">
            <el-form-item label="预览">
              <el-image :src="formView.accessUrl" fit="contain" style="width:100px"></el-image>
            </el-form-item>
          </el-col>
          <el-col :lg="24">
            <el-form-item label="访问路径">{{formView.accessUrl}}
              <el-button class="copy-btn-main" icon="el-icon-document-copy" type="text" v-clipboard:copy="formView.accessUrl"
                v-clipboard:success="clipboardSuccess">
                复制
              </el-button>
            </el-form-item>
          </el-col>
          <el-col :lg="24">
            <el-form-item label="存储路径">{{formView.fileUrl}}</el-form-item>
          </el-col>
        </el-row>
      </el-form>
    </el-dialog>
  </div>
</template>
<script>
import { listSysfile, delSysfile, getSysfile } from '@/api/tool/file.js'

export default {
  name: 'sysfile',
  data() {
    return {
      labelWidth: '100px',
      formLabelWidth: '100px',
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
        fileId: undefined
      },
      // 弹出层标题
      title: '',
      // 是否显示弹出层
      open: false,
      openView: false,
      // 表单参数
      form: {},
      formView: {},
      columns: [],
      // 上传时间时间范围
      dateRangeAddTime: [],
      // 存储类型选项列表
      storeTypeOptions: [
        { dictLabel: '本地存储', dictValue: 1 },
        { dictLabel: '阿里云存储', dictValue: 2 }
      ],
      // 存储类型 1、本地 2、阿里云
      storeType: 0,
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
            message: '上传文件不能为空',
            trigger: 'blur'
          }
        ],
        storeType: [
          {
            required: true,
            message: '存储类型不能为空',
            trigger: 'blur'
          }
        ]
      }
    }
  },
  created() {
    // 列表数据查询
    this.getList()
  },
  methods: {
    // 查询数据
    getList() {
      this.addDateRange(this.queryParams, this.dateRangeAddTime, 'Create_time')
      this.loading = true
      listSysfile(this.queryParams).then((res) => {
        if (res.code == 200) {
          this.dataList = res.data.result
          this.total = res.data.totalNum
          this.loading = false
        }
      })
    },
    // 取消按钮
    cancel() {
      this.open = false
      this.reset()
    },
    // 重置数据表单
    reset() {
      this.form = {
        fileName: '',
        fileUrl: '',
        storePath: 'uploads',
        fileSize: 0,
        fileExt: '',
        storeType: 1,
        accessUrl: ''
      }
      this.resetForm('form')
    },
    /** 重置查询操作 */
    resetQuery() {
      this.timeRange = []
      // 上传时间时间范围
      this.dateRangeAddTime = []
      this.resetForm('queryForm')
      this.handleQuery()
    },
    // 多选框选中数据
    handleSelectionChange(selection) {
      this.ids = selection.map((item) => item.id)
      this.single = selection.length != 1
      this.multiple = !selection.length
    },
    /** 搜索按钮操作 */
    handleQuery() {
      this.queryParams.pageNum = 1
      this.getList()
    },
    /** 新增按钮操作 */
    handleAdd() {
      this.reset()
      this.open = true
      this.title = '上传文件'
      this.form.storeType = this.queryParams.storeType
    },
    /** 删除按钮操作 */
    handleDelete(row) {
      const Ids = row.id || this.ids

      this.$confirm('是否确认删除参数编号为"' + Ids + '"的数据项？')
        .then(function() {
          return delSysfile(Ids)
        })
        .then(() => {
          this.handleQuery()
          this.msgSuccess('删除成功')
        })
        .catch(() => {})
    },
    /** 查看按钮操作 */
    handleView(row) {
      const id = row.id || this.ids
      getSysfile(id).then((res) => {
        const { code, data } = res
        if (code == 200) {
          this.openView = true
          this.formView = data
        }
      })
    },
    // 上传成功方法
    handleUploadSuccess(columnName, filelist, data) {
      this.form[columnName] = filelist
      this.open = false
      this.getList()
    },
    // 手动上传
    submitUpload() {
      this.$refs.upload.submitUpload()
    },
    // 存储类型字典翻译
    storeTypeFormat(row, column) {
      return this.selectDictLabel(this.storeTypeOptions, row.storeType)
    },
    /** 复制代码成功 */
    clipboardSuccess() {
      this.msgSuccess('复制成功')
    }
  }
}
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