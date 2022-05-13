<template>
  <div class="app-container">
    <el-form ref="codeform" :inline="true" :rules="rules" :model="queryParams" size="small">
      <el-form-item label="表名" prop="tableName">
        <el-input v-model="queryParams.tableName" clearable placeholder="输入要查询的表名" />
      </el-form-item>
      <el-form-item>
        <el-button size="mini" type="primary" icon="el-icon-search" @click="handleSearch()">查询</el-button>
        <!-- <el-button type="default" icon="el-icon-refresh" size="small" @click="loadTableData()">刷新</el-button> -->
      </el-form-item>
    </el-form>

    <el-row :gutter="10" class="mb10">
      <el-col :span="1.5">
        <el-button type="info" plain icon="el-icon-upload" size="mini" @click="openImportTable" v-hasPermi="['tool:gen:import']">导入</el-button>
      </el-col>
      <el-col :span="1.5">
        <el-button
          type="danger"
          :disabled="multiple"
          plain
          icon="el-icon-delete"
          @click="handleDelete"
          size="mini"
          v-hasPermi="['tool:gen:remove']"
        >
          删除
        </el-button>
      </el-col>
    </el-row>
    <el-table
      ref="gridtable"
      v-loading="tableloading"
      :data="tableData"
      border
      @selection-change="handleSelectionChange"
      highlight-current-row
      height="480px"
    >
      <el-table-column type="selection" align="center" width="55"></el-table-column>
      <el-table-column label="序号" type="index" width="50" align="center">
        <template slot-scope="scope">
          <span>{{ (queryParams.pageNum - 1) * queryParams.pageSize + scope.$index + 1 }}</span>
        </template>
      </el-table-column>
      <el-table-column prop="dbName" label="数据库名" width="90" :show-overflow-tooltip="true" />
      <el-table-column prop="tableId" label="表id" width="70" sortable="" />
      <el-table-column prop="tableName" label="表名" width="110" :show-overflow-tooltip="true" />
      <el-table-column prop="tableComment" label="表描述" :show-overflow-tooltip="true" width="120" />
      <el-table-column prop="className" label="实体" :show-overflow-tooltip="true" />
      <el-table-column prop="createTime" label="创建时间" />
      <el-table-column prop="updateTime" label="更新时间" sortable />
      <el-table-column label="操作" align="center" width="320">
        <template slot-scope="scope">
          <el-button type="text" icon="el-icon-view" @click="handlePreview(scope.row)" v-hasPermi="['tool:gen:preview']">预览</el-button>
          <el-button type="text" icon="el-icon-edit" @click="handleEditTable(scope.row)" v-hasPermi="['tool:gen:edit']">编辑</el-button>
          <el-button type="text" icon="el-icon-delete" @click="handleDelete(scope.row)" v-hasPermi="['tool:gen:remove']">删除</el-button>
          <el-button type="text" icon="el-icon-refresh" @click="handleSynchDb(scope.row)" v-hasPermi="['tool:gen:edit']">同步</el-button>
          <el-button type="text" icon="el-icon-download" @click="handleGenTable(scope.row)" v-hasPermi="['tool:gen:code']">生成代码 </el-button>
        </template>
      </el-table-column>
    </el-table>
    <pagination :page.sync="queryParams.pageNum" :limit.sync="queryParams.pageSize" :total="total" @pagination="handleSearch" />

    <!-- 预览界面 -->
    <el-dialog :title="preview.title" :visible.sync="preview.open" width="80%" top="5vh" append-to-body>
      <el-tabs v-model="preview.activeName">
        <el-tab-pane v-for="(item, key) in preview.data" :label="item.title" :name="key.toString()" :key="key">
          <el-link
            :underline="false"
            icon="el-icon-document-copy"
            v-clipboard:copy="item.content"
            v-clipboard:success="clipboardSuccess"
            style="float: right"
          >
            复制
          </el-link>
          <pre><code class="hljs" v-html="highlightedCode(item.content, item.title)"></code></pre>
        </el-tab-pane>
      </el-tabs>
    </el-dialog>
    <import-table ref="import" @ok="handleSearch" />
  </div>
</template>

<script>
import { codeGenerator, listTable, delTable, previewTable, synchDb } from '@/api/tool/gen'
import importTable from './importTable'
import { Loading } from 'element-ui'
import hljs from 'highlight.js'
import 'highlight.js/styles/idea.css' // 这里有多个样式，自己可以根据需要切换

export default {
  name: 'gen',
  components: { importTable, hljs },
  data() {
    return {
      queryParams: {
        pageNum: 1,
        pageSize: 20,
        tableName: '',
      },
      // 预览参数
      preview: {
        open: false,
        title: '代码预览',
        data: {},
        activeName: '0',
      },
      showGenerate: false,
      rules: {},
      // 表数据
      tableData: [],
      // 是否显示加载
      tableloading: false,
      total: 0,
      // 选中行的表
      currentSelected: {},
      // 选中的列
      checkedQueryColumn: [],
      // 是否覆盖原先代码
      coverd: true,
      // 选中的表
      tableIds: [],
      // 非多个禁用
      multiple: true,
    }
  },
  created() {
    this.handleSearch()
  },
  methods: {
    /**
     * 点击查询
     */
    handleSearch() {
      this.tableloading = true

      listTable(this.queryParams).then((res) => {
        this.tableData = res.data.result
        this.total = res.data.totalNum
        this.tableloading = false
      })
    },
    /**
     * 编辑表格
     */
    handleEditTable(row) {
      this.queryParams.tableName = row.tableName
      this.handleSearch()

      this.$router.push({
        path: '/gen/editTable',
        query: { tableId: row.tableId },
      })
    },
    // 代码预览
    handlePreview(row) {
      this.$refs['codeform'].validate((valid) => {
        if (!valid) {
          this.msgError('请先完成表格')
          return
        }
        this.$modal.loading('请稍后...')
        previewTable(row.tableId).then((res) => {
          if (res.code === 200) {
            this.showGenerate = false
            this.preview.open = true
            this.preview.data = res.data
            this.$modal.closeLoading()
          }
        })
      })
    },
    /**
     * 点击生成服务端代码
     */
    handleGenTable(row) {
      this.currentSelected = row
      if (!this.currentSelected) {
        this.msgError('请先选择要生成代码的数据表')
        return false
      }
      this.$refs['codeform'].validate((valid) => {
        if (valid) {
          var loadop = {
            lock: true,
            text: '正在生成代码...',
            spinner: 'el-icon-loading',
            background: 'rgba(0, 0, 0, 0.7)',
          }
          const pageLoading = Loading.service(loadop)

          var seachdata = {
            tableId: this.currentSelected.tableId,
            tableName: this.currentSelected.name,
            // queryColumn: this.checkedQueryColumn,
          }

          codeGenerator(seachdata)
            .then((res) => {
              const { data } = res
              this.showGenerate = false
              if (row.genType === '1') {
                this.msgSuccess('成功生成到自定义路径')
              } else {
                this.msgSuccess('恭喜你，代码生成完成！')
                this.download(data.path)
              }
              pageLoading.close()
            })
            .catch((erre) => {
              pageLoading.close()
            })
        } else {
          return false
        }
      })
    },
    cancel() {
      this.showGenerate = false
      this.currentSelected = {}
    },
    /** 重置按钮操作 */
    resetQuery() {
      this.resetForm('queryParams')
      this.handleSearch()
    },
    /** 打开导入表弹窗 */
    openImportTable() {
      this.$refs.import.show()
    },
    handleDelete(row) {
      const tableIds = row.tableId || this.tableIds
      this.$confirm('此操作将永久删除该文件, 是否继续?', '提示', {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning',
      })
        .then(() => {
          delTable(tableIds.toString()).then((res) => {
            if (res.code == 200) {
              this.msgSuccess('删除成功')

              this.handleSearch()
            }
          })
        })
        .catch(() => {
          this.$message({
            type: 'info',
            message: '已取消删除',
          })
        })
    },
    /** 复制代码成功 */
    clipboardSuccess() {
      this.msgSuccess('复制成功')
    },
    // 多选框选中数据
    handleSelectionChange(section) {
      this.tableIds = section.map((item) => item.tableId)
      this.multiple = !section.length
      console.log(this.tableIds)
    },
    /** 高亮显示 */
    highlightedCode(code, key) {
      // var language = key.substring(key.lastIndexOf(".") , key.length)
      const result = hljs.highlightAuto(code || '')
      return result.value || '&nbsp;'
    },
    // 同步代码
    handleSynchDb(row) {
      const tableName = row.tableName
      this.$confirm('确认要强制同步"' + tableName + '"表结构吗？')
        .then(function () {
          return synchDb(row.tableId, { tableName, dbName: row.dbName })
        })
        .then(() => {
          this.msgSuccess('同步成功')
        })
        .catch(() => {})
    },
  },
}
</script>
