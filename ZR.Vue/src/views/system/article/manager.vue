<template>
  <div class="app-container">
    <el-row :gutter="24">
      <!-- :model属性用于表单验证使用 比如下面的el-form-item 的 prop属性用于对表单值进行验证操作 -->
      <el-form :model="queryParams" label-position="left" inline ref="queryForm" label-width="100px" v-show="showSearch" @submit.native.prevent>
        <el-col :span="6">
          <el-form-item label="文章标题" prop="title">
            <el-input v-model="queryParams.title" placeholder="请输入文章标题" size="small" />
          </el-form-item>
        </el-col>
        <el-col :span="6">
          <el-form-item label="文章状态" prop="status">
            <el-select v-model="queryParams.status" size="small">
              <el-option v-for="item in statusOptions" :key="item.dictValue" :label="item.dictLabel" :value="item.dictValue"></el-option>
            </el-select>
          </el-form-item>
        </el-col>
        <el-form-item>
          <el-button type="primary" icon="el-icon-search" size="mini" @click="handleQuery">搜索</el-button>
          <el-button icon="el-icon-refresh" size="mini" @click="resetQuery">重置</el-button>
        </el-form-item>
      </el-form>
    </el-row>

    <el-row :gutter="10" class="mb8">
      <el-col :span="1.5">
        <el-button type="primary" plain icon="el-icon-plus" size="mini" v-hasPermi="['system:article:add']" @click="handleAdd">发布文章</el-button>
      </el-col>
      <right-toolbar :showSearch.sync="showSearch"></right-toolbar>
    </el-row>

    <el-table :data="dataList" ref="table" border>
      <el-table-column prop="cid" label="id" width="60" sortable> </el-table-column>
      <el-table-column prop="title" label="文章标题" width="180" :show-overflow-tooltip="true"> </el-table-column>
      <el-table-column prop="authorName" label="作者" width="80"> </el-table-column>
      <el-table-column prop="fmt_type" label="编辑器类型" width="100"> </el-table-column>
      <el-table-column prop="tags" label="标签" width="100" :show-overflow-tooltip="true"> </el-table-column>
      <el-table-column prop="hits" label="点击量" width="80" align="center"> </el-table-column>
      <el-table-column prop="content" label="文章内容" :show-overflow-tooltip="true"> </el-table-column>
      <el-table-column sortable prop="status" align="center" label="状态" width="90">
        <template slot-scope="scope">
          <el-tag size="mini" :type="scope.row.status == '2' ? 'danger' : 'success'" disable-transitions>{{ scope.row.status == '2' ? "草稿":"已发布" }}
          </el-tag>
        </template>
      </el-table-column>

      <el-table-column prop="createTime" label="创建时间" width="128" :show-overflow-tooltip="true"> </el-table-column>
      <el-table-column label="操作" align="center" width="190">
        <template slot-scope="scope">
          <el-button size="mini" type="text" icon="el-icon-view" @click="handleView(scope.row)">查看</el-button>
          <el-button size="mini" type="text" icon="el-icon-edit" @click="handleUpdate(scope.row)" v-hasPermi="['system:article:update']">编辑
          </el-button>
          <el-popconfirm title="确定删除吗？" @onConfirm="handleDelete(scope.row)" style="margin-left:10px">
            <el-button slot="reference" size="mini" type="text" icon="el-icon-delete" v-hasPermi="['system:article:delete']">删除</el-button>
          </el-popconfirm>
        </template>
      </el-table-column>
    </el-table>
    <pagination :total="total" :page.sync="queryParams.pageNum" :limit.sync="queryParams.pageSize" @pagination="getList" />

  </div>
</template>
<script>
import { listArticle, delArticle } from '@/api/system/article.js'

export default {
  name: 'articleindex',
  data() {
    return {
      // 遮罩层
      loading: true,
      // 显示搜索条件
      showSearch: true,
      // 查询参数
      queryParams: {},
      // 弹出层标题
      title: '',
      // 是否显示弹出层
      open: false,
      // 表单参数
      form: {},
      // 文章状态下拉框
      statusOptions: [],
      // 数据列表
      dataList: [],
      // 总记录数
      total: 0,
      // 提交按钮是否显示
      btnSubmitVisible: true,
			// 文章预览地址
			previewUrl: ''
    }
  },
  created() {
    this.getList()
    this.getDicts('sys_article_status').then((response) => {
      this.statusOptions = response.data
    })

    this.getConfigKey('sys.article.preview.url').then((response) => {
      this.previewUrl = response.data
    })
  },
  methods: {
    // 查询数据
    getList() {
      listArticle(this.queryParams).then((res) => {
        if (res.code == 200) {
          this.dataList = res.data.result
          this.total = res.data.totalNum
        }
      })
    },
    /** 重置查询操作 */
    resetQuery() {
      this.resetForm('queryForm')
    },
    /** 搜索按钮操作 */
    handleQuery() {
      this.getList()
    },
    /** 新增按钮操作 */
    handleAdd() {
      this.$router.replace({ path: '/article/publish' })
    },
    /** 删除按钮操作 */
    handleDelete(row) {
      delArticle(row.cid).then((res) => {
        if (res.code == 200) {
          this.msgSuccess('删除成功')
          this.handleQuery()
        }
      })
    },
    /** 修改按钮操作 */
    handleUpdate(row) {
      this.$router.push({ path: '/article/publish', query: { cid: row.cid }})
    },
    // 详情
    handleView(row) {
			var link = `${this.previewUrl}${row.cid}`
			window.open(link)
    },
    handleImport() {},
    handleExport() {}
  }
}
</script>
