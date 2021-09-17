<template>
  <div class="app-container">

    <el-form ref="codeform" :inline="true" :rules="rules" :model="queryParams" size="small">
      <!-- <el-form-item label="数据库" prop="dbName">
        <el-select v-model="queryParams.dbName" clearable placeholder="请选择" @change="handleShowTable">
          <el-option v-for="item in selectedDataBase" :key="item" :label="item" :value="item" />
        </el-select>
      </el-form-item> -->
      <el-form-item label="表名">
        <el-input v-model="queryParams.tableName" clearable placeholder="输入要查询的表名" />
      </el-form-item>
      <!-- <el-form-item label="项目命名空间：" prop="baseSpace">
            <el-tooltip class="item" effect="dark" content="系统会根据项目命名空间自动生成IService、Service、Models等子命名空间" placement="bottom">
              <el-input v-model="queryParams.baseSpace" clearable placeholder="如Zr" />
            </el-tooltip>
          </el-form-item> -->
      <el-form-item label="去掉表名前缀：">
        <el-tooltip class="item" effect="dark" content="表名直接变为类名，去掉表名前缀。" placement="bottom">
          <el-input v-model="queryParams.replaceTableNameStr" clearable width="300" placeholder="例如：sys_" />
        </el-tooltip>
      </el-form-item>
      <el-form-item>
        <el-button type="primary" @click="handleSearch()">查询</el-button>
        <el-button type="default" icon="el-icon-refresh" size="small" @click="loadTableData()">刷新</el-button>
      </el-form-item>
    </el-form>

    <el-row :gutter="10" class="mb10">
      <el-col :span="1.5">
        <el-button type="info" plain icon="el-icon-upload" size="mini" @click="openImportTable" v-hasPermi="['tool:gen:import']">导入</el-button>
      </el-col>

      <el-col :span="1.5">
        <el-button type="danger" plain icon="el-icon-delete" size="mini" v-hasPermi="['tool:gen:delete']">删除</el-button>
      </el-col>
    </el-row>
    <el-table ref="gridtable" v-loading="tableloading" :data="tableData" border stripe highlight-current-row height="500px" style="width: 100%;">
      <el-table-column type="selection" align="center" width="55"></el-table-column>
      <el-table-column label="序号" type="index" width="50" align="center">
        <template slot-scope="scope">
          <span>{{(queryParams.pageNum - 1) * queryParams.pageSize + scope.$index + 1}}</span>
        </template>
      </el-table-column>
      <el-table-column prop="tableName" label="表名" sortable="custom" width="380" />
      <el-table-column prop="tableComment" label="表描述" />
      <el-table-column prop="className" label="实体" />
      <el-table-column prop="createTime" label="创建时间" />
      <el-table-column prop="updateTime" label="更新时间" />
      <el-table-column label="操作" align="center" width="240">
        <template slot-scope="scope">
          <el-button type="text" icon="el-icon-view" @click="handlePreview()">预览</el-button>
          <el-button type="text" icon="el-icon-edit" @click="handleEditTable(scope.row)">编辑</el-button>
          <el-button type="text" icon="el-icon-download" @click="handleShowDialog(scope.row)" v-hasPermi="['tool:gen:code']">生成代码</el-button>
        </template>
      </el-table-column>
    </el-table>
    <pagination :page.sync="queryParams.pageNum" :limit.sync="queryParams.pageSize" :total="total" @pagination="handleSearch" />

    <import-table ref="import" @ok="handleSearch" />

    <el-dialog :visible.sync="showGenerate" title="代码生成" width="800px">
      <el-form ref="codeGenerateForm" label-width="140px">
        <el-form-item label="要生成的文件">
          <el-checkbox-group v-model="checkedCodeGenerateForm">
            <el-checkbox :label="1">生成Model</el-checkbox>
            <el-checkbox :label="2">生成Dto</el-checkbox>
            <el-checkbox :label="3">生成Repository</el-checkbox>
            <el-checkbox :label="4">生成Service</el-checkbox>
            <el-checkbox :label="5">生成Controller</el-checkbox>
            <el-checkbox :label="6">生成Views和api</el-checkbox>
          </el-checkbox-group>
        </el-form-item>

        <el-form-item label="是否覆盖生成">
          <el-radio v-model="coverd" :label="true">是</el-radio>
          <el-radio v-model="coverd" :label="false">否</el-radio>
        </el-form-item>

      </el-form>
      <div slot="footer" class="dialog-footer">
        <el-button type="primary" @click="handleGenerate">确 定</el-button>
        <el-button @click="cancel">取 消</el-button>
      </div>
    </el-dialog>
  </div>
</template>

<script>
import { codeGenerator, getGenTable } from "@/api/tool/gen";
import importTable from "./importTable";
import { Loading } from "element-ui";

export default {
  name: "CodeGenerator",
  components: { importTable },
  data() {
    return {
      queryParams: {
        pageNum: 1,
        pageSize: 20,
        dbName: "",
        tableName: "",
        baseSpace: "",
        replaceTableNameStr: "",
      },
      showGenerate: false,
      checkedCodeGenerateForm: [1, 2, 3, 4, 5, 6],
      rules: {
        dbName: [
          { required: true, message: "请选择数据库名称", trigger: "blur" },
        ],
        replaceTableNameStr: [
          { min: 0, max: 50, message: "长度小于50个字符", trigger: "blur" },
        ],
      },
      // 表数据
      tableData: [],
      // 是否显示加载
      tableloading: false,
      total: 0,
      // 选中行的表
      currentSelected: {},
      selectedDataBase: [],
      // 列信息
      // columnData: [],
      // 选中的列
      checkedQueryColumn: [],
      //是否覆盖原先代码
      coverd: true,
    };
  },
  created() {
    this.handleSearch();
  },
  methods: {
    /**
     * 点击查询
     */
    handleSearch() {
      this.tableloading = true;

      getGenTable(this.queryParams).then((res) => {
        this.tableData = res.data.result;
        this.total = res.data.totalCount;
        this.tableloading = false;
      });
    },
    /**
     * 编辑表格
     */
    handleEditTable(row) {
      console.log(row);
      this.$router.push("/tool/editTable?tableId=" + row.tableId);
    },
    handlePreview() {
      this.msgError("敬请期待");
    },
    handleShowDialog(row) {
      this.showGenerate = true;
      this.currentSelected = row;

    },
    /**
     * 点击生成服务端代码
     */
    handleGenerate: async function () {
      console.log(JSON.stringify(this.currentSelected));
      if (!this.currentSelected) {
        this.msgError("请先选择要生成代码的数据表");
        return false;
      }
      this.$refs["codeform"].validate((valid) => {
        if (valid) {
          var loadop = {
            lock: true,
            text: "正在生成代码...",
            spinner: "el-icon-loading",
            background: "rgba(0, 0, 0, 0.7)",
          };
          const pageLoading = Loading.service(loadop);

          var seachdata = {
            dbName: this.codeform.dbName,
            tableName: this.currentSelected.name,
            baseSpace: this.codeform.baseSpace,
            replaceTableNameStr: this.codeform.replaceTableNameStr,
            genFiles: this.checkedCodeGenerateForm,
            coverd: this.coverd,
            queryColumn: this.checkedQueryColumn,
          };
          console.log(JSON.stringify(seachdata));

          codeGenerator(seachdata)
            .then((res) => {
              if (res.code == 200) {
                // downloadFile(
                //   defaultSettings.fileUrl + res.ResData[0],
                //   res.ResData[1]
                // );
                this.showGenerate = false;
                this.msgSuccess("恭喜你，代码生成完成！");
              } else {
                this.msgError(res.msg);
              }
              pageLoading.close();
            })
            .catch((erre) => {
              pageLoading.close();
            });
        } else {
          return false;
        }
      });
    },
    /**
     * 选择每页显示数量
     */
    // handleSizeChange(val) {
    //   this.pagination.pagesize = val;
    //   this.pagination.pageNum = 1;
    //   this.loadTableData();
    // },
    /**
     * 选择当页面
     */
    // handleCurrentChange(val) {
    //   this.pagination.pageNum = val;
    //   this.loadTableData();
    // },
    cancel() {
      this.showGenerate = false;
      this.currentSelected = {};
    },
    // 导入代码生成
    openImportTable() {
      this.$refs.import.show();
    },
  },
};
</script>
