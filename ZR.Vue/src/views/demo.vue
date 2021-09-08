<template>
  <div class="app-container">
    <el-row :gutter="24">
      <!-- :model属性用于表单验证使用 比如下面的el-form-item 的 prop属性用于对表单值进行验证操作 -->
      <el-form :model="queryParams" label-position="left" inline ref="queryForm" :label-width="labelWidth" v-show="showSearch" @submit.native.prevent>
        <el-col :span="6">
          <el-form-item label="文本文字">
            <el-input v-model="queryParams.xxx" placeholder="" />
          </el-form-item>
        </el-col>
        <el-col :span="6">
          <el-form-item label="数字">
            <el-input v-model.number="queryParams.xxx" placeholder="" />
          </el-form-item>
        </el-col>
        <el-col :span="6">
          <el-form-item label="下拉框">
            <el-select v-model="queryParams.xxx" placeholder="">
              <el-option v-for="dict in statusOptions" :key="dict.dictValue" :label="dict.dictLabel" :value="dict.dictValue" />
            </el-select>
          </el-form-item>
        </el-col>
        <el-col :span="6">
          <el-form-item label="时间范围">
            <el-date-picker size="small" style="width: 240px" v-model="timeRange" value-format="yyyy-MM-dd" type="daterange" range-separator="-" start-placeholder="开始日期" end-placeholder="结束日期">
            </el-date-picker>
          </el-form-item>
        </el-col>
        <el-col :span="24" style="text-align:center;">
          <el-button type="primary" icon="el-icon-search" size="mini" @click="handleQuery">搜索</el-button>
          <el-button icon="el-icon-refresh" size="mini" @click="resetQuery">重置</el-button>
        </el-col>
      </el-form>
    </el-row>

    <!-- 工具区域 -->
    <el-row :gutter="10" class="mb8">
      <el-col :span="1.5">
        <el-button type="primary" v-hasPermi="['Article:add']" plain icon="el-icon-plus" size="mini" @click="handleAdd">新增</el-button>
      </el-col>
      <el-col :span="1.5">
        <el-button type="success" v-hasPermi="['Article:update']" plain icon="el-icon-edit" size="mini" @click="handleUpdate">修改</el-button>
      </el-col>
      <el-col :span="1.5">
        <el-button type="danger" v-hasPermi="['Article:delete']" plain icon="el-icon-delete" size="mini" @click="handleDelete">删除</el-button>
      </el-col>
      <!-- <el-col :span="1.5">
        <el-button type="info" plain icon="el-icon-upload2" size="mini" @click="handleImport">导入</el-button>
      </el-col>
      <el-col :span="1.5">
        <el-button type="warning" plain icon="el-icon-download" size="mini" @click="handleExport">导出</el-button>
      </el-col> -->
      <right-toolbar :showSearch.sync="showSearch" @queryTable="getList"></right-toolbar>
    </el-row>

    <!-- 数据区域 -->
    <el-table :data="dataList" ref="table" border @selection-change="handleSelectionChange">
      <el-table-column type="selection" width="50" />
      <el-table-column prop="cid" label="Cid" align="center" width="100" />
      <el-table-column prop="title" label="文章标题" align="center" width="100" :show-overflow-tooltip="true" />
      <el-table-column prop="content" label="文章内容" align="center" width="100" />
      <el-table-column prop="userId" label="用户id" align="center" width="100" />
      <el-table-column prop="status" label="文章状态1、已发布 2、草稿" align="center" width="100" :show-overflow-tooltip="true" />
      <el-table-column prop="fmt_type" label="编辑器类型markdown,html" align="center" width="100" :show-overflow-tooltip="true" />
      <el-table-column prop="tags" label="文章标签" align="center" width="100" :show-overflow-tooltip="true" />
      <el-table-column prop="hits" label="点击量" align="center" width="100" />
      <el-table-column prop="category_id" label="目录id" align="center" width="100" />
      <el-table-column prop="createTime" label="创建时间" align="center" width="100" />
      <el-table-column prop="updateTime" label="修改时间" align="center" width="100" />
      <el-table-column prop="authorName" label="作者名" align="center" width="100" :show-overflow-tooltip="true" />

      <el-table-column label="操作" align="center" width="200">
        <template slot-scope="scope">
          <el-button size="mini" type="text" icon="el-icon-view" @click="handleView(scope.row)">详情</el-button>
          <el-button size="mini" v-hasPermi="['Article:update']" type="text" icon="el-icon-edit" @click="handleUpdate(scope.row)">编辑</el-button>
          <el-popconfirm title="确定删除吗？" @onConfirm="handleDelete(scope.row)" style="margin-left:10px">
            <el-button slot="reference" v-hasPermi="['Article:del']" size="mini" type="text" icon="el-icon-delete">删除</el-button>
          </el-popconfirm>
        </template>
      </el-table-column>
    </el-table>
    <pagination :total="total" :page.sync="queryParams.pageNum" :limit.sync="queryParams.pageSize" @pagination="getList" />

    <!-- 添加或修改菜单对话框 -->
    <el-dialog :title="title" :visible.sync="open" width="600px" append-to-body>
      <el-form ref="form" :model="form" :rules="rules" label-width="80px">
        <el-col :span="12">
          <el-form-item label="Cid" :label-width="labelWidth" prop="cid">
            <el-input v-model="form.cid" placeholder="请输入Cid" />
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="文章标题" :label-width="labelWidth" prop="title">
            <el-input v-model="form.title" placeholder="请输入文章标题" />
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="文章内容" :label-width="labelWidth" prop="content">
            <el-input v-model="form.content" placeholder="请输入文章内容" />
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="用户id" :label-width="labelWidth" prop="userId">
            <el-input v-model="form.userId" placeholder="请输入用户id" />
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="文章状态1、已发布 2、草稿" :label-width="labelWidth" prop="status">
            <el-input v-model="form.status" placeholder="请输入文章状态1、已发布 2、草稿" />
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="编辑器类型markdown,html" :label-width="labelWidth" prop="fmt_type">
            <el-input v-model="form.fmt_type" placeholder="请输入编辑器类型markdown,html" />
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="文章标签" :label-width="labelWidth" prop="tags">
            <el-input v-model="form.tags" placeholder="请输入文章标签" />
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="点击量" :label-width="labelWidth" prop="hits">
            <el-input v-model="form.hits" placeholder="请输入点击量" />
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="目录id" :label-width="labelWidth" prop="category_id">
            <el-input v-model="form.category_id" placeholder="请输入目录id" />
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="作者名" :label-width="labelWidth" prop="authorName">
            <el-input v-model="form.authorName" placeholder="请输入作者名" />
          </el-form-item>
        </el-col>

      </el-form>
      <div slot="footer" class="dialog-footer" v-if="btnSubmitVisible">
        <el-button type="primary" @click="submitForm">确 定</el-button>
        <el-button @click="cancel">取 消</el-button>
      </div>
    </el-dialog>

  </div>
</template>
<script>
// import { listArticle,addArticle,delArticle,editArticle } from '@/api/Article.js'

export default {
  name: "Article",
  data() {
    return {
      labelWidth: "70px",
      // 选中数组
      ids: [],
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
        Cid: [{ required: true, message: "请输入", trigger: "blur" }],
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
      listArticle(this.addDateRange(this.queryParams, this.timeRange)).then(
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
        Cid: "",
        Title: "",
        Content: "",
        UserId: "",
        Status: "",
        Fmt_type: "",
        Tags: "",
        Hits: "",
        Category_id: "",
        CreateTime: "",
        UpdateTime: "",
        AuthorName: "",

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
        //TODO 重置字段
      };
    },
    // 多选框选中数据
    handleSelectionChange(selection) {
      this.ids = selection.map((item) => item.Cid);
      this.multiple = !selection.length;
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

      //TODO 业务代码
    },
    /** 删除按钮操作 */
    handleDelete(row) {
      delArticle().then((res) => {
        this.msgSuccess("删除成功");
        this.handleQuery();
      });
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
          if (this.form.Cid != undefined) {
            //TODO 编辑业务代码
          } else {
            //TODO 新增业务代码
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
<style scoped>
.table-td-thumb {
  width: 80px;
}
</style>
