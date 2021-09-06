<template>
  <div class="app-container">
    <!-- <div class="filter-container">
      <el-card>
        <el-form ref="searchDbform" :inline="true" :model="searchform" class="demo-form-inline" size="small">
          <el-form-item label="数据库地址" prop="DbAddress">
            <el-input v-model="searchDbform.DbAddress" placeholder="请输入数据库地址" autocomplete="off" clearable />
          </el-form-item>
          <el-form-item label="数据库名称" prop="DbName">
            <el-input v-model="searchDbform.DbName" placeholder="请输入数据库名称" autocomplete="off" clearable />
          </el-form-item>
          <el-form-item label="用户名" prop="DbUserName">
            <el-input v-model="searchDbform.DbUserName" placeholder="请输入用户名" autocomplete="off" clearable />
          </el-form-item>
          <el-form-item label="访问密码" prop="DbPassword">
            <el-input v-model="searchDbform.DbPassword" placeholder="请输入访问密码" autocomplete="off" clearable />
          </el-form-item>
          <el-form-item label="数据库类型" prop="DbType">
            <el-select v-model="searchDbform.DbType" clearable placeholder="请选数据库类型">
              <el-option v-for="item in selectDbTypes" :key="item.Id" :label="item.Title" :value="item.Id" />
            </el-select>
          </el-form-item>
          <el-form-item label="数据库端口" prop="DbPort">
            <el-input v-model="searchDbform.DbPort" placeholder="请输入数据库端口" autocomplete="off" clearable />
          </el-form-item>
          <el-form-item>
            <el-button type="primary" @click="handleDbConn()">链接</el-button>
          </el-form-item>
        </el-form>
      </el-card>
    </div> -->
    <el-card>
      <div class="list-btn-container">
        <el-form ref="codeform" :inline="true" :rules="rules" :model="codeform" class="demo-form-inline" size="small">
          <el-form-item label="数据库">
            <el-tooltip class="item" effect="dark" content="默认为系统访问数据库" placement="top">
              <el-select v-model="searchform.DbName" clearable placeholder="请选择" @change="handleShowTable">
                <el-option v-for="item in selectedDataBase" :key="item.Id" :label="item.dbName" :value="item.dbName" />
              </el-select>
            </el-tooltip>
          </el-form-item>
          <el-form-item label="数据库表名">
            <el-input v-model="searchform.tableName" clearable placeholder="输入要查询的表名" />
          </el-form-item>
          <el-form-item>
            <el-button type="primary" @click="handleSearch()">查询</el-button>
          </el-form-item>
          <el-form-item label="项目命名空间：" prop="baseSpace">
            <el-tooltip class="item" effect="dark" content="系统会根据项目命名空间自动生成IService、Service、Models等子命名空间" placement="bottom">
              <el-input v-model="codeform.baseSpace" clearable placeholder="如Zr" />
            </el-tooltip>
          </el-form-item>
          <el-form-item label="去掉表名前缀：">
            <el-tooltip class="item" effect="dark" content="表名直接变为类名，去掉表名前缀。多个前缀用“;”隔开和结束" placement="bottom">
              <el-input v-model="codeform.replaceTableNameStr" clearable width="300" placeholder="多个前缀用“;”隔开" />
            </el-tooltip>
          </el-form-item>
          <el-form-item>
            <el-button type="primary" icon="iconfont icon-code" @click="handleGenerate()">生成代码</el-button>
            <el-button type="default" icon="el-icon-refresh" size="small" @click="loadTableData()">刷新</el-button>
          </el-form-item>
        </el-form>
      </div>
      <el-table ref="gridtable" v-loading="tableloading" :data="tableData" border stripe highlight-current-row style="width: 100%" :default-sort="{prop: 'TableName', order: 'ascending'}" @select="handleSelectChange" @select-all="handleSelectAllChange" @sort-change="handleSortChange">
        <el-table-column type="selection" width="50" />
        <el-table-column prop="tableName" label="表名" sortable="custom" width="380" />
        <el-table-column prop="description" label="表描述" />
      </el-table>
      <div class="pagination-container">
        <el-pagination background :current-page="pagination.pageNum" :page-sizes="[5,10,20,50,100, 200, 300, 400]" :page-size="pagination.pagesize" layout="total, sizes, prev, pager, next, jumper" :total="pagination.pageTotal" @size-change="handleSizeChange" @current-change="handleCurrentChange" />
      </div>
    </el-card>
  </div>
</template>

<script>
import {
  createGetDBConn,
  codeGetDBList,
  codeGetTableList,
  codeGenerator,
} from "@/api/tool/gen";
import { downloadFile } from "@/utils/index";
import { Loading } from "element-ui";

import defaultSettings from "@/settings";
export default {
  name: "CodeGenerator",
  data() {
    return {
      searchDbform: {
        DbName: "",
        DbAddress: "",
        DbPort: "1433",
        DbUserName: "",
        DbPassword: "",
        DbType: "",
      },
      searchform: {
        DbName: "",
        tableName: "",
      },
      codeform: {
        baseSpace: "",
        replaceTableNameStr: "",
      },
      rules: {
        baseSpace: [
          { required: true, message: "请输入项目名称", trigger: "blur" },
          {
            min: 2,
            max: 50,
            message: "长度在 2 到 50 个字符",
            trigger: "blur",
          },
        ],
        replaceTableNameStr: [
          { min: 0, max: 50, message: "长度小于50个字符", trigger: "blur" },
        ],
      },
      selectDbTypes: [
        {
          Id: "SqlServer",
          Title: "SqlServer",
        },
        {
          Id: "MySql",
          Title: "MySql",
        },
      ],
      tableData: [],
      tableloading: false,
      pagination: {
        pageNum: 1,
        pagesize: 20,
        pageTotal: 0,
      },
      sortableData: {
        order: "",
        sort: "",
      },
      currentSelected: [],
      selectedDataBase: [],
    };
  },
  created() {
    this.pagination.pageNum = 1;
    this.loadData();
    // this.loadTableData();
  },
  methods: {
    loadData: function () {
      codeGetDBList().then((res) => {
        this.selectedDataBase = res.data;
      });
    },
    /**
     * 加载页面table数据
     */
    loadTableData: function () {
      if (this.searchform.dataBaseName !== "") {
        this.tableloading = true;
        var seachdata = {
          pageNum: this.pagination.pageNum,
          PageSize: this.pagination.pagesize,
          Keywords: this.searchform.tableName,
          EnCode: this.searchform.DbName,
          // Order: this.sortableData.order,
          // Sort: this.sortableData.sort,
        };
        codeGetTableList(seachdata).then((res) => {
          this.tableData = res.data.result;
          this.pagination.pageTotal = res.data.totalNum;
          this.tableloading = false;
        });
      }
    },
    /**
     * 点击查询
     */
    handleSearch: function () {
      this.tableloading = true;
      this.pagination.pageNum = 1;
      this.loadTableData();
    },
    handleShowTable: function () {
      this.pagination.pageNum = 1;
      // this.loadTableData();
    },
    handleDbConn: function () {
      var dataInfo = {
        DbAddress: this.searchDbform.DbAddress,
        DbPort: this.searchDbform.DbPort,
        DbName: this.searchDbform.DbName,
        DbUserName: this.searchDbform.DbUserName,
        DbPassword: this.searchDbform.DbPassword,
        DbType: this.searchDbform.DbType,
      };
      createGetDBConn(dataInfo).then((res) => {
        this.selectedDataBase = res.ResData;
        this.searchform.DbName = this.searchDbform.DbName;
      });
      this.pagination.pageNum = 1;
      this.loadTableData();
    },
    /**
     * 点击生成服务端代码
     */
    handleGenerate: async function () {
      if (this.currentSelected.length === 0) {
        this.$alert("请先选择要生成代码的数据表", "提示");
        return false;
      } else {
        this.$refs["codeform"].validate((valid) => {
          if (valid) {
            var loadop = {
              lock: true,
              text: "正在生成代码...",
              spinner: "el-icon-loading",
              background: "rgba(0, 0, 0, 0.7)",
            };
            const pageLoading = Loading.service(loadop);
            var currentTables = "";
            this.currentSelected.forEach((element) => {
              currentTables += element.TableName + ",";
            });
            var seachdata = {
              tables: currentTables,
              baseSpace: this.codeform.baseSpace,
              replaceTableNameStr: this.codeform.replaceTableNameStr,
            };
            codeGenerator(seachdata)
              .then((res) => {
                if (res.code == 100) {
                  downloadFile(
                    defaultSettings.fileUrl + res.ResData[0],
                    res.ResData[1]
                  );

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
      }
    },
    /**
     * 当表格的排序条件发生变化的时候会触发该事件
     */
    handleSortChange: function (column) {
      this.sortableData.sort = column.prop;
      if (column.order === "ascending") {
        this.sortableData.order = "asc";
      } else {
        this.sortableData.order = "desc";
      }
      this.loadTableData();
    },
    /**
     * 当用户手动勾选checkbox数据行事件
     */
    handleSelectChange: function (selection, row) {
      this.currentSelected = selection;
    },
    /**
     * 当用户手动勾选全选checkbox事件
     */
    handleSelectAllChange: function (selection) {
      this.currentSelected = selection;
    },
    /**
     * 选择每页显示数量
     */
    handleSizeChange(val) {
      this.pagination.pagesize = val;
      this.pagination.pageNum = 1;
      this.loadTableData();
    },
    /**
     * 选择当页面
     */
    handleCurrentChange(val) {
      this.pagination.pageNum = val;
      this.loadTableData();
    },
  },
};
</script>
