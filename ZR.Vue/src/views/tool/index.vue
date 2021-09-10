<template>
  <div class="app-container">
    <el-card>
      <div>
        <el-form ref="codeform" :inline="true" :rules="rules" :model="codeform" size="small">
          <el-form-item label="数据库" prop="dbName">
            <el-select v-model="codeform.dbName" clearable placeholder="请选择" @change="handleShowTable">
              <el-option v-for="item in selectedDataBase" :key="item" :label="item" :value="item" />
            </el-select>
          </el-form-item>
          <el-form-item label="表名">
            <el-input v-model="codeform.tableName" clearable placeholder="输入要查询的表名" />
          </el-form-item>
          <!-- <el-form-item label="项目命名空间：" prop="baseSpace">
            <el-tooltip class="item" effect="dark" content="系统会根据项目命名空间自动生成IService、Service、Models等子命名空间" placement="bottom">
              <el-input v-model="codeform.baseSpace" clearable placeholder="如Zr" />
            </el-tooltip>
          </el-form-item> -->
          <el-form-item label="去掉表名前缀：">
            <el-tooltip class="item" effect="dark" content="表名直接变为类名，去掉表名前缀。" placement="bottom">
              <el-input v-model="codeform.replaceTableNameStr" clearable width="300" placeholder="例如：sys_" />
            </el-tooltip>
          </el-form-item>
          <el-form-item>
            <el-button type="primary" @click="handleSearch()">查询</el-button>
            <el-button type="default" icon="el-icon-refresh" size="small" @click="loadTableData()">刷新</el-button>
          </el-form-item>
        </el-form>
      </div>
      <el-table ref="gridtable" v-loading="tableloading" :data="tableData" border stripe highlight-current-row height="500px" style="width: 100%;">
        <!-- <el-table-column type="selection" width="50" /> -->
        <el-table-column prop="name" label="表名" sortable="custom" width="380" />
        <el-table-column prop="description" label="表描述" />
        <el-table-column label="操作" align="center" width="200">
          <template slot-scope="scope">
            <el-button type="text" icon="el-icon-view" @click="handlePreview()">预览</el-button>
            <el-button type="text" icon="el-icon-download" @click="handleShowDialog(scope.row)">生成代码</el-button>
          </template>
        </el-table-column>
      </el-table>
      <el-pagination background :current-page="pagination.pageNum" :page-sizes="[5,10,20,50,100, 200, 300, 400]" :page-size="pagination.pagesize" layout="total, sizes, prev, pager, next, jumper" :total="pagination.pageTotal" @size-change="handleSizeChange" @current-change="handleCurrentChange" />
    </el-card>

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
        <!-- <el-form-item label="生成查询的列">
          <el-table :data="columnData" height="300px">
            <el-table-column type="selection" width="60" />
            <el-table-column label="字段列名" prop="dbColumnName" />
            <el-table-column label="字段描述" prop="columnDescription">
              <template slot-scope="scope">
                <el-input v-model="scope.row.columnDescription" />
              </template>
            </el-table-column>
            <el-table-column label="表数据类型" prop="dataType" />
            <el-table-column label="C#类型">
              <template slot-scope="scope">
                <el-select v-model="scope.row.dataType">
                  <el-option value="int">int</el-option>
                  <el-option value="bigint">bigint</el-option>
                  <el-option value="varchar">varchar</el-option>
                </el-select>
              </template>
            </el-table-column>
            <el-table-column label="显示类型">
              <el-select v-model="selectType">
                <el-option value="input">文本框</el-option>
                <el-option value="textArea">文本域</el-option>
                <el-option value="select">下拉框</el-option>
                <el-option value="radio">单选框</el-option>
                <el-option value="datetime">日期控件</el-option>
                <el-option value="upload">图片上传</el-option>
                <el-option value="fileUpload">文件上传</el-option>
              </el-select>
            </el-table-column>
          </el-table>
        </el-form-item> -->
      </el-form>
      <div slot="footer" class="dialog-footer">
        <el-button type="primary" @click="handleGenerate">确 定</el-button>
        <el-button @click="cancel">取 消</el-button>
      </div>
    </el-dialog>
  </div>
</template>

<script>
import {
  // createGetDBConn,
  codeGetDBList,
  codeGetTableList,
  codeGenerator,
  queryColumnInfo,
} from "@/api/tool/gen";
// import { downloadFile } from "@/utils/index";
import { Loading } from "element-ui";

export default {
  name: "CodeGenerator",
  data() {
    return {
      codeform: {
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
      tableData: [],
      tableloading: false,
      pagination: {
        pageNum: 1,
        pagesize: 20,
        pageTotal: 0,
      },
      // 选中行的表
      currentSelected: {},
      selectedDataBase: [],
      // 列信息
      columnData: [],
      // 选中的列
      checkedQueryColumn: [],
      //是否覆盖原先代码
      coverd: true,
    };
  },
  created() {
    this.pagination.pageNum = 1;
    this.loadData();
    this.loadTableData();
  },
  methods: {
    loadData() {
      codeGetDBList().then((res) => {
        const { dbList, defaultDb } = res.data;
        this.codeform.dbName = defaultDb;
        this.selectedDataBase = dbList;
      });
    },
    /**
     * 加载页面table数据
     */
    loadTableData() {
      if (this.codeform.dbName !== "") {
        this.tableloading = true;
        var seachdata = {
          pageNum: this.pagination.pageNum,
          PageSize: this.pagination.pagesize,
          tableName: this.codeform.tableName,
          dbName: this.codeform.dbName,
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
    handleSearch() {
      this.$refs["codeform"].validate((valid) => {
        if (valid) {
          this.tableloading = true;
          this.pagination.pageNum = 1;
          this.loadTableData();
        } else {
          return false;
        }
      });
    },
    handleShowTable() {
      this.pagination.pageNum = 1;
      this.loadTableData();
    },
    handlePreview() {
      this.msgError("敬请期待");
    },
    handleShowDialog(row) {
      this.showGenerate = true;
      this.currentSelected = row;

      queryColumnInfo({
        dbName: this.codeform.dbName,
        tableName: row.name,
      }).then((res) => {
        if (res.code === 200) {
          const columnData = res.data;
          this.columnData = columnData;
        }
      });
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
    cancel() {
      this.showGenerate = false;
      this.currentSelected = {};
    },
  },
};
</script>
