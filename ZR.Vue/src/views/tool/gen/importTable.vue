<template>
  <!-- 导入表 -->
  <el-dialog title="导入表" :visible.sync="visible" width="900px" top="5vh" append-to-body>
    <el-form ref="queryParams" :inline="true" :rules="rules" :model="queryParams" size="small">
      <el-form-item label="数据库" prop="dbName">
        <el-select v-model="queryParams.dbName" clearable placeholder="请选择" @change="handleShowTable">
          <el-option v-for="item in dbList" :key="item" :label="item" :value="item" />
        </el-select>
      </el-form-item>
      <el-form-item label="表名">
        <el-input v-model="queryParams.tableName" clearable placeholder="输入要查询的表名" />
      </el-form-item>
      <el-form-item>
        <el-button type="primary" @click="handleQuery()">查询</el-button>
        <!-- <el-button type="default" icon="el-icon-refresh" size="small" @click="loadTableData()">刷新</el-button> -->
      </el-form-item>
    </el-form>
    <el-row>
      <el-table ref="table" @row-click="clickRow" :data="dbTableList" highlight-current-row height="300px" @selection-change="handleSelectionChange">
        <el-table-column type="selection" width="55"></el-table-column>
        <el-table-column prop="name" label="表名" sortable="custom" width="380" />
        <el-table-column prop="description" label="表描述" />
      </el-table>
      <pagination background :page.sync="queryParams.pageNum" :limit.sync="queryParams.pageSize" layout="total, sizes, prev, pager, next, jumper" :total="total" @pagination="getList" />

    </el-row>
    <div slot="footer" class="dialog-footer">
      <el-button type="primary" @click="handleImportTable">确 定</el-button>
      <el-button @click="visible = false">取 消</el-button>
    </div>
  </el-dialog>
</template>

<script>
import { listDbTable, importTable, codeGetDBList } from "@/api/tool/gen";

export default {
  data() {
    return {
      // 遮罩层
      visible: false,
      // 选中数组值
      tables: [],
      // 总条数
      total: 0,
      // 表数据
      dbTableList: [],
      // 数据库数据
      dbList: [],
      // 查询参数
      queryParams: {
        dbName: "",
        pageNum: 1,
        pageSize: 10,
        tableName: undefined,
      },
      rules: {
        dbName: [
          { required: true, message: "请选择数据库名称", trigger: "blur" },
        ]
      },
    };
  },
  methods: {
    // 显示弹框
    show() {
      this.getList();
      this.visible = true;
    },
    clickRow(row) {
      this.$refs.table.toggleRowSelection(row);
    },
    // 多选框选中数据
    handleSelectionChange(selection) {
      this.tables = selection.map((item) => item.name);
    },
    // 查询表数据
    getList() {
      codeGetDBList().then((res) => {
        const { dbList } = res.data;
        this.dbList = dbList;
      });
      if (this.queryParams.dbName !== "") {
        listDbTable(this.queryParams).then((res) => {
          this.dbTableList = res.data.result;
          this.total = res.data.totalNum;
        });
      }
    },
    /** 搜索按钮操作 */
    handleQuery() {
      this.queryParams.pageNum = 1;
      this.getList();
    },
    /** 重置按钮操作 */
    resetQuery() {
      this.resetForm("queryForm");
      this.handleQuery();
    },
    handleShowTable() {
      this.handleQuery();
    },
    /** 导入按钮操作 */
    handleImportTable() {
      console.log(JSON.stringify(this.tables));

      importTable({
        tables: this.tables.join(","),
        dbName: this.queryParams.dbName,
      }).then((res) => {
        this.msgSuccess(res.msg);
        if (res.code === 200) {
          this.visible = false;
          this.$emit("ok");
        }
      });
    },
  },
};
</script>
