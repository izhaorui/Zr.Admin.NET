<template>
  <div class="app-container">
    <el-row :gutter="24">
      <!-- :model属性用于表单验证使用 比如下面的el-form-item 的 prop属性用于对表单值进行验证操作 -->
      <el-form :model="queryParams" label-position="left" inline ref="queryForm" v-show="showSearch" @submit.native.prevent>
        <el-col :span="6">
          <el-form-item label="文本">
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
              <el-option v-for="dict in options" :key="dict.dictValue" :label="dict.dictLabel" :value="dict.dictValue" />
            </el-select>
          </el-form-item>
        </el-col>
        <el-col :span="6">
          <el-form-item label="时间范围">
            <el-date-picker v-model="queryParams.timeRange" type="datetimerange" range-separator="至" start-placeholder="开始日期" end-placeholder="结束日期">
            </el-date-picker>
          </el-form-item>
        </el-col>
        <el-col :span="24" style="text-align:center;">
          <el-button type="cyan" icon="el-icon-search" size="mini" @click="handleQuery">搜索</el-button>
          <el-button icon="el-icon-refresh" size="mini" @click="resetQuery">重置</el-button>
        </el-col>
      </el-form>
    </el-row>

    <!-- 工具区域 -->
    <el-row :gutter="10" class="mb8">
      <el-col :span="1.5">
        <el-button type="primary" plain icon="el-icon-plus" size="mini" @click="handleAdd">新增</el-button>
      </el-col>
      <el-col :span="1.5">
        <el-button type="success" plain icon="el-icon-edit" size="mini" @click="handleUpdate">修改</el-button>
      </el-col>
      <el-col :span="1.5">
        <el-button type="danger" plain icon="el-icon-delete" size="mini" @click="handleDelete">删除</el-button>
      </el-col>
      <el-col :span="1.5">
        <el-button type="info" plain icon="el-icon-upload2" size="mini" @click="handleImport">导入</el-button>
      </el-col>
      <el-col :span="1.5">
        <el-button type="warning" plain icon="el-icon-download" size="mini" @click="handleExport">导出</el-button>
      </el-col>
      <right-toolbar :showSearch.sync="showSearch"></right-toolbar>
    </el-row>

    <!-- 数据区域 -->
    <el-table :data="dataList" ref="table" border>
      <el-table-column prop="id" label="id" width="60" sortable> </el-table-column>
      <el-table-column prop="userId" label="userid" width="80"> </el-table-column>
      <!-- 显示图片 -->
      <el-table-column prop="photo" label="图片" width="110">
        <template slot-scope="scope">
          <el-popover placement="right" trigger="hover">
            <!-- click显示的大图 -->
            <img :src="scope.row.photo" />
            <img slot="reference" :src="scope.row.photo" width="100" height="50">
          </el-popover>
        </template>
      </el-table-column>
      <el-table-column prop="content" label="介绍" width="100" :show-overflow-tooltip="true">
      </el-table-column>
      <el-table-column sortable prop="isStart" align="center" label="状态" width="90">
        <template>
          <el-tag size="mini" type="success" disable-transitions>标签</el-tag>
        </template>
      </el-table-column>

      <el-table-column prop="addtime" label="创建时间"> </el-table-column>
      <el-table-column label="操作" align="center" width="200">
        <template slot-scope="scope">
          <el-button size="mini" type="text" icon="el-icon-view" @click="handleView(scope.row)">详情</el-button>
          <el-button size="mini" type="text" icon="el-icon-edit" @click="handleUpdate(scope.row)">编辑</el-button>
          <el-popconfirm title="确定删除吗？" @onConfirm="handleDelete(scope.row)" style="margin-left:10px">
            <el-button slot="reference" size="mini" type="text" icon="el-icon-delete">删除</el-button>
          </el-popconfirm>
        </template>
      </el-table-column>
    </el-table>
    <pagination :total="total" :page.sync="queryParams.pageNum" :limit.sync="queryParams.pageSize" @pagination="getList" />

    <!-- 添加或修改菜单对话框 -->
    <el-dialog :title="title" :visible.sync="open" width="600px" append-to-body>
      <el-form ref="form" :model="form" :rules="rules" label-width="100px">
        <el-row>
          <el-col :span="12">
            <el-form-item label="用户Id" prop="userId">
              <el-input v-model.number="form.userId" placeholder="" :disabled="form.userId > 0" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="用户昵称" prop="name">
              <el-input v-model="form.name" placeholder="" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="顺序" prop="sortId">
              <el-input-number v-model="form.sortId" controls-position="right" :min="0" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="状态" prop="status">
              <el-radio-group v-model="form.status">
                <el-radio v-for="dict in options" :key="dict.dictValue" :label="dict.dictValue">{{dict.dictLabel}}</el-radio>
              </el-radio-group>
            </el-form-item>
          </el-col>
          <el-col :span="24">
            <el-form-item label="备注" prop="content">
              <el-input v-model="form.content" :rows="2" type="textarea" placeholder="请输入内容" />
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
// import { listXXXX} from '@/api/xxxx.js'

export default {
  name: "demo",
  data() {
    return {
      // 遮罩层
      loading: true,
      // 显示搜索条件
      showSearch: true,
      // 查询参数
      queryParams: {
        useridx: undefined,
        name: undefined,
      },
      // 弹出层标题
      title: "",
      // 是否显示弹出层
      open: false,
      // 表单参数
      form: {},
      // xxx下拉框
      options: [],
      // 数据列表
      dataList: [
        {
          id: 1,
          photo:
            "https://ss1.baidu.com/-4o3dSag_xI4khGko9WTAnF6hhy/zhidao/pic/item/d788d43f8794a4c2b124d0000df41bd5ad6e3991.jpg",
          name: "你好",
          userId: 1000001,
          sortId: 1,
          address: "浙江省杭州市西湖区",
          content: "我是一个超长超长的文字啊",
          addtime: "2021-8-7 23:00:00",
        },
      ],
      // 总记录数
      total: 0,
      // 提交按钮是否显示
      btnSubmitVisible: true,
      // 表单校验
      rules: {
        name: [{ required: true, message: "名称不能为空", trigger: "blur" }],
        userId: [{ required: true, message: "id不能为空", trigger: "blur" }],
      },
    };
  },
  mounted() {
    this.getList();
  },
  methods: {
    // 查询数据
    getList() {
      console.log(JSON.stringify(this.queryParams));
      // listXXXX().then(res => {
      //   if (res.code == 200) {
      //     this.dataList = res.data.result;
      //     this.total = res.data.totalCount;
      //   }
      // })
    },
    // 取消按钮
    cancel() {
      this.open = false;
      this.reset();
    },
    // 重置数据表单
    reset() {
      this.btnSubmitVisible = true;
      this.form = {
        Id: undefined,
        // TODO 其他列字段
      };
      this.resetForm("form");
    },
    /** 重置查询操作 */
    resetQuery() {
      this.resetForm("queryForm");
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
      //   delXXX().then(function () {

      //   })
      //   .then(() => {
      //     this.msgSuccess("删除成功");
      //   });
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
          if (this.form.Id != undefined) {
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
