<template>
  <div class="app-container">
    <el-form :model="queryParams" ref="queryForm" v-show="showSearch" :inline="true">
      <el-form-item label="角色名称" prop="roleName">
        <el-input v-model="queryParams.roleName" placeholder="请输入角色名称" clearable size="small" style="width: 240px" @keyup.enter.native="handleQuery" />
      </el-form-item>
      <!-- <el-form-item label="权限字符" prop="roleKey">
        <el-input v-model="queryParams.roleKey" placeholder="请输入权限字符" clearable size="small" style="width: 240px" @keyup.enter.native="handleQuery" />
      </el-form-item> -->
      <el-form-item label="状态" prop="status">
        <el-select v-model="queryParams.status" placeholder="角色状态" clearable size="small" style="width: 240px">
          <el-option v-for="dict in statusOptions" :key="dict.dictValue" :label="dict.dictLabel" :value="dict.dictValue" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button type="primary" icon="el-icon-search" size="mini" @click="handleQuery">搜索</el-button>
        <el-button icon="el-icon-refresh" size="mini" @click="resetQuery">重置</el-button>
      </el-form-item>
    </el-form>

    <el-row :gutter="10" class="mb8">
      <el-col :span="1.5">
        <el-button type="primary" plain icon="el-icon-plus" size="mini" @click="handleAdd" v-hasPermi="['system:role:add']">新增</el-button>
      </el-col>
       <el-col :span="1.5">
        <el-button type="warning" icon="el-icon-download" size="mini" @click="handleExport" v-hasPermi="['system:role:export']">导出</el-button>
      </el-col>
      <right-toolbar :showSearch.sync="showSearch" @queryTable="getList"></right-toolbar>
    </el-row>

    <el-row :gutter="24" style="display:flex;">
      <el-col :span="15">
        <el-table v-loading="loading" :data="roleList" border @selection-change="handleSelectionChange">
          <el-table-column label="编号" prop="roleId" width="80" />
          <el-table-column label="名称" prop="roleName" />
          <el-table-column label="权限字符" prop="roleKey"/>
          <el-table-column label="状态" align="center">
            <template slot-scope="scope">
              <el-switch v-model="scope.row.status" active-value="0" inactive-value="1" @change="handleStatusChange(scope.row)"></el-switch>
            </template>
          </el-table-column>
          <el-table-column label="创建时间" align="center" prop="createTime" width="150"/>
          <el-table-column label="操作" align="center" width="200">
            <template slot-scope="scope">
              <el-button size="mini" type="text" icon="el-icon-edit" @click.stop="handleUpdate(scope.row)" v-if="scope.row.roleKey != 'admin'" v-hasPermi="['system:role:edit']">修改</el-button>
              <el-button size="mini" type="text" icon="el-icon-delete" @click.stop="handleDelete(scope.row)" v-if="scope.row.roleKey != 'admin'" v-hasPermi="['system:role:remove']">删除</el-button>
              <el-button size="mini" type="text" icon="el-icon-circle-check" @click.stop="handleDataScope(scope.row)" v-if="scope.row.roleKey != 'admin'" v-hasPermi="['system:role:authorize']">数据权限</el-button>
            </template>
          </el-table-column>
        </el-table>

        <!-- <pagination :total="total" :page.sync="queryParams.pageNum" :limit.sync="queryParams.pageSize" @pagination="getList" /> -->
      </el-col>
      <el-col :span="9">
        <el-form :model="form" v-show="showRoleScope" label-width="80px" style="width:90%">
          <el-form-item label="权限字符">
            {{form.roleKey}}
          </el-form-item>
          <el-form-item label="数据权限" style="max-height:350px;overflow:auto;">
            <el-checkbox v-model="menuExpand" @change="handleCheckedTreeExpand($event, 'menu')">展开/折叠</el-checkbox>
            <el-checkbox v-model="menuNodeAll" @change="handleCheckedTreeNodeAll($event, 'menu')">全选/全不选</el-checkbox>
            <el-checkbox v-model="form.menuCheckStrictly" @change="handleCheckedTreeConnect($event, 'menu')">父子联动</el-checkbox>
            <el-tree class="tree-border" :data="menuOptions" show-checkbox ref="menu" node-key="id" :check-strictly="!form.menuCheckStrictly" empty-text="加载中，请稍后" :props="defaultProps"></el-tree>
          </el-form-item>
          <el-form-item>
            <el-button type="primary" @click="submitDataScope" v-hasPermi="['system:role:authorize']">保存</el-button>
          </el-form-item>
        </el-form>

      </el-col>
    </el-row>

    <!-- 添加或修改角色配置对话框 -->
    <el-dialog :title="title" :visible.sync="open" width="500px" append-to-body>
      <el-form ref="form" :model="form" :rules="rules" label-width="80px">
        <el-form-item label="角色名称" prop="roleName">
          <el-input v-model="form.roleName" placeholder="请输入角色名称" />
        </el-form-item>
        <el-form-item label="权限字符" prop="roleKey">
          <el-input v-model="form.roleKey" placeholder="请输入权限字符" />
        </el-form-item>
        <el-form-item label="角色顺序" prop="roleSort">
          <el-input-number v-model="form.roleSort" controls-position="right" :min="0" />
        </el-form-item>
        <el-form-item label="状态">
          <el-radio-group v-model="form.status">
            <el-radio v-for="dict in statusOptions" :key="dict.dictValue" :label="dict.dictValue">{{ dict.dictLabel }}</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="form.remark" type="textarea" placeholder="请输入内容"></el-input>
        </el-form-item>
      </el-form>
      <div slot="footer" class="dialog-footer">
        <el-button type="primary" @click="submitForm">确 定</el-button>
        <el-button @click="cancel">取 消</el-button>
      </div>
    </el-dialog>

  </div>
</template>

<script>
import {
  listRole,
  getRole,
  delRole,
  addRole,
  updateRole,
  exportRole,
  dataScope,
  changeRoleStatus,
} from "@/api/system/role";
import {
  treeselect as menuTreeselect,
  roleMenuTreeselect,
} from "@/api/system/menu";
// import {
//   treeselect as deptTreeselect,
//   roleDeptTreeselect,
// } from "@/api/system/dept";
import { downloadFile } from "@/utils/zipdownload.js";

export default {
  name: "Role",
  data() {
    return {
      // 添加、修改模式是否显示菜单分配
      showMenuTree: false,
      // 遮罩层
      loading: true,
      // 选中数组
      ids: [],
      // 非单个禁用
      single: true,
      // 非多个禁用
      multiple: true,
      // 显示搜索条件
      showSearch: true,
      // 表格高度
      tableHeight: window.innerHeight,
      // 总条数
      total: 0,
      // 角色表格数据
      roleList: [],
      // 弹出层标题
      title: "",
      // 是否显示弹出层
      open: false,
      menuExpand: true,
      menuNodeAll: false,
      deptExpand: true,
      deptNodeAll: false,
      // 日期范围
      dateRange: [],
      // 状态数据字典
      statusOptions: [],
      // 是否下那是菜单分配
      showRoleScope: false,
      // 数据范围选项
      dataScopeOptions: [
        {
          value: "1",
          label: "全部数据权限",
        },
        {
          value: "2",
          label: "自定数据权限",
        },
        {
          value: "3",
          label: "本部门数据权限",
        },
        {
          value: "4",
          label: "本部门及以下数据权限",
        },
        {
          value: "5",
          label: "仅本人数据权限",
        },
      ],
      // 菜单列表
      menuOptions: [],
      // 部门列表
      deptOptions: [],
      // 查询参数
      queryParams: {
        pageNum: 1,
        pageSize: 10,
        roleName: undefined,
        roleKey: undefined,
        status: undefined,
      },
      // 表单参数
      form: {},
      defaultProps: {
        children: "children",
        label: "label",
      },
      // 表单校验
      rules: {
        roleName: [
          { required: true, message: "角色名称不能为空", trigger: "blur" },
        ],
        roleKey: [
          { required: true, message: "权限字符不能为空", trigger: "blur" },
        ],
        roleSort: [
          { required: true, message: "角色顺序不能为空", trigger: "blur" },
        ],
      },
    };
  },
  created() {
    this.getList();
    this.getDicts("sys_normal_disable").then((response) => {
      this.statusOptions = response.data;
    });
  },
  methods: {
    /** 查询角色列表 */
    getList() {
      this.loading = true;

      listRole(this.addDateRange(this.queryParams, this.dateRange)).then(
        (response) => {
          this.roleList = response.data.result;
          this.total = response.data.totalNum;
          this.loading = false;
        }
      );
    },
    /** 查询菜单树结构 */
    getMenuTreeselect() {
      menuTreeselect().then((response) => {
        this.menuOptions = response.data;
      });
    },
    /** 查询部门树结构 */
    // getDeptTreeselect() {
    //   deptTreeselect().then((response) => {
    //     this.deptOptions = response.data;
    //   });
    // },
    // 所有菜单节点数据
    getMenuAllCheckedKeys() {
      // 目前被选中的菜单节点
      let checkedKeys = this.$refs.menu.getCheckedKeys();
      // 半选中的菜单节点
      let halfCheckedKeys = this.$refs.menu.getHalfCheckedKeys();
      checkedKeys.unshift.apply(checkedKeys, halfCheckedKeys);
      return checkedKeys;
    },
    // 所有部门节点数据
    // getDeptAllCheckedKeys() {
    //   // 目前被选中的部门节点
    //   let checkedKeys = this.$refs.dept.getCheckedKeys();
    //   // 半选中的部门节点
    //   let halfCheckedKeys = this.$refs.dept.getHalfCheckedKeys();
    //   checkedKeys.unshift.apply(checkedKeys, halfCheckedKeys);
    //   return checkedKeys;
    // },
    /** 根据角色ID查询菜单树结构 */
    getRoleMenuTreeselect(roleId) {
      return roleMenuTreeselect(roleId).then((response) => {
        this.menuOptions = response.data.menus;
        return response;
      });
    },
    /** 根据角色ID查询部门树结构 */
    // getRoleDeptTreeselect(roleId) {
    //   return roleDeptTreeselect(roleId).then((response) => {
    //     this.deptOptions = response.depts;
    //     return response;
    //   });
    // },
    // 角色状态修改
    handleStatusChange(row) {
      let text = row.status === "0" ? "启用" : "停用";

      console.log(JSON.stringify(row), text);
      this.$confirm(
        '确认要"' + text + '""' + row.roleName + '"角色吗?',
        "警告",
        {
          confirmButtonText: "确定",
          cancelButtonText: "取消",
          type: "warning",
        }
      )
        .then(function () {
          return changeRoleStatus(row.roleId, row.status);
        })
        .then(() => {
          this.msgSuccess(text + "成功");
        })
        .catch(function () {
          row.status = row.status === "0" ? "1" : "0";
        });
    },
    // 取消按钮
    cancel() {
      this.open = false;
      this.reset();
    },
    // 表单重置
    reset() {
      if (this.$refs.menu != undefined) {
        this.$refs.menu.setCheckedKeys([]);
      }
      (this.menuExpand = false),
        (this.menuNodeAll = false),
        (this.deptExpand = true),
        (this.deptNodeAll = false),
        (this.form = {
          roleId: undefined,
          roleName: undefined,
          roleKey: undefined,
          roleSort: 0,
          status: "0",
          menuIds: [],
          deptIds: [],
          menuCheckStrictly: true,
          deptCheckStrictly: true,
          remark: undefined,
        });
      this.resetForm("form");
    },
    /** 搜索按钮操作 */
    handleQuery() {
      this.queryParams.pageNum = 1;
      this.getList();
    },
    /** 重置按钮操作 */
    resetQuery() {
      this.dateRange = [];
      this.resetForm("queryForm");
      this.handleQuery();
    },
    // 多选框选中数据
    handleSelectionChange(selection) {
      this.ids = selection.map((item) => item.roleId);
      this.single = selection.length != 1;
      this.multiple = !selection.length;
    },
    // 树权限（展开/折叠）
    handleCheckedTreeExpand(value, type) {
      if (type == "menu") {
        let treeList = this.menuOptions;
        for (let i = 0; i < treeList.length; i++) {
          this.$refs.menu.store.nodesMap[treeList[i].id].expanded = value;
        }
      } else if (type == "dept") {
        let treeList = this.deptOptions;
        for (let i = 0; i < treeList.length; i++) {
          this.$refs.dept.store.nodesMap[treeList[i].id].expanded = value;
        }
      }
    },
    // 树权限（全选/全不选）
    handleCheckedTreeNodeAll(value, type) {
      if (type == "menu") {
        this.$refs.menu.setCheckedNodes(value ? this.menuOptions : []);
      } else if (type == "dept") {
        this.$refs.dept.setCheckedNodes(value ? this.deptOptions : []);
      }
    },
    // 树权限（父子联动）
    handleCheckedTreeConnect(value, type) {
      if (type == "menu") {
        this.form.menuCheckStrictly = value ? true : false;
      } else if (type == "dept") {
        this.form.deptCheckStrictly = value ? true : false;
      }
    },
    /** 新增按钮操作 */
    handleAdd() {
      this.reset();
      // this.getMenuTreeselect();
      this.open = true;
      this.title = "添加角色";
      this.showRoleScope = false;
      this.showMenuTree = true;
    },
    /** 修改按钮操作 ok */
    handleUpdate(row) {
      this.reset();
      this.showRoleScope = false;
      this.showMenuTree = false; //新加
      const roleId = row.roleId || this.ids;
      // const roleMenu = this.getRoleMenuTreeselect(roleId);
      getRole(roleId).then((response) => {
        this.form = response.data;
        this.open = true;
        this.title = "修改角色";
      });
    },
    /** 分配角色权限按钮操作 */
    //新增 和上面代码基本相同
    handleDataScope(row) {
      if (row.roleId == 1) {
        this.showRoleScope = false;
        return;
      }
      this.reset();
      this.showRoleScope = true;
      const roleId = row.roleId || this.ids;
      const roleMenu = this.getRoleMenuTreeselect(roleId);

      roleMenu.then((res) => {
        let checkedKeys = res.data.checkedKeys;
        checkedKeys.forEach((v) => {
          this.$nextTick(() => {
            this.$refs.menu.setChecked(v, true, false);
          });
        });
      });
      this.form = {
        roleId: row.roleId,
        roleName: row.roleName,
        roleKey: row.roleKey,
        menuCheckStrictly: true
      };
    },
    /** 提交按钮 */
    submitForm: function () {
      this.$refs["form"].validate((valid) => {
        if (valid) {
          if (this.form.roleId != undefined && this.form.roleId > 0) {
            this.form.type = "edit";
            // this.form.menuIds = this.getMenuAllCheckedKeys();
            updateRole(this.form).then((response) => {
              this.msgSuccess("修改成功");
              this.open = false;
              this.getList();
            });
          } else {
            this.form.type = "add";
            this.form.menuIds = this.getMenuAllCheckedKeys();
            addRole(this.form).then((response) => {
              console.log(response);
              this.open = false;
              if (response.code == 200) {
                this.msgSuccess("新增成功");
                this.getList();
              } else {
                this.msgInfo(response.msg);
              }
            });
          }
        }
      });
    },
    /** 提交按钮（菜单数据权限） */
    submitDataScope: function () {
      if (this.form.roleId != undefined) {
        this.form.menuIds = this.getMenuAllCheckedKeys();
        // this.form.deptIds = this.getDeptAllCheckedKeys();
        dataScope(this.form).then((response) => {
          this.msgSuccess("修改成功");
          this.getList();
          this.handleDataScope({ roleId: this.form.roleId });
        });
      } else {
        this.msgError("请选择角色");
      }
    },
    /** 删除按钮操作 */
    handleDelete(row) {
      const roleIds = row.roleId || this.ids;
      this.$confirm(
        '是否确认删除角色编号为"' + roleIds + '"的数据项?',
        "警告",
        {
          confirmButtonText: "确定",
          cancelButtonText: "取消",
          type: "warning",
        }
      )
        .then(function () {
          return delRole(roleIds);
        })
        .then(() => {
          this.getList();
          this.msgSuccess("删除成功");
        });
    },
    /** 导出按钮操作 */
    handleExport() {
      const queryParams = this.queryParams;
      this.$confirm("是否确认导出所有角色数据项?", "警告", {
        confirmButtonText: "确定",
        cancelButtonText: "取消",
        type: "warning",
      })
        .then(function () {
          return exportRole(queryParams);
        })
        .then((response) => {
          this.download(response.data.path);
        });
    },
  },
};
</script>
