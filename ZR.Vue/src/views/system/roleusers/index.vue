<template>
  <div class="app-container">

    <!-- <el-col :span="4" :xs="24">
      <el-table ref="roleTable" v-loading="loadingRole" highlight-current-row :data="dataRoleTable" border :height="tableHeight-135"
        @row-click="handleRoleTableSelection">
        <el-table-column prop="roleName" label="请选择角色名称" />
      </el-table>
    </el-col> -->

    <el-form :inline="true" @submit.native.prevent>
      <el-form-item label="角色名">
        <el-input v-model="roleUserQueryParams.roleName" disabled />
      </el-form-item>
      <el-form-item label="角色字符串">
        <el-input v-model="roleUserQueryParams.roleKey" disabled />
      </el-form-item>
      <el-form-item label="用户名">
        <el-input v-model="roleUserQueryParams.userName" placeholder="请输入用户名称" clearable prefix-icon="el-icon-search"
          @keyup.enter.native="searchRoleUser" />
      </el-form-item>
      <el-form-item>
        <el-button type="primary" icon="el-icon-search" size="mini" @click="searchRoleUser">搜索</el-button>
        <!-- <el-button icon="el-icon-refresh" size="mini" @click="resetQuery">重置</el-button> -->
      </el-form-item>
    </el-form>

    <el-row :gutter="10" class="mb8">
      <el-col :span="1.5">
        <el-button type="primary" plain size="mini" icon="el-icon-plus" @click="handleGetUserTable" v-hasPermi="['system:roleusers:add']">添加用户
        </el-button>
      </el-col>
      <el-col :span="1.5">
        <el-button type="danger" plain size="mini" icon="el-icon-circle-close" @click="cancelAuthUserAll" v-hasPermi="['system:roleusers:remove']">
          批量取消授权</el-button>
      </el-col>
      <el-col :span="1.5">
        <el-button type="warning" plain icon="el-icon-close" size="mini" @click="handleClose">关闭</el-button>
      </el-col>
    </el-row>

    <el-table ref="roleUserTable" v-loading="loadingRoleUser" :data="dataRoleUserTable" row-key="userId" stripe border>
      <el-table-column type="selection" width="55" align="center" />
      <el-table-column prop="userId" align="center" label="用户Id" width="150" />
      <el-table-column prop="userName" align="center" label="用户名" width="150" />
      <el-table-column prop="nickName" align="center" label="用户昵称" width="150" />
      <el-table-column prop="email" align="center" label="邮箱" />
      <el-table-column prop="status" align="center" label="账号状态" width="80">
        <template slot-scope="scope">
					<dict-tag :options="statusOptions" :value="scope.row.status" />
        </template>
      </el-table-column>
      <el-table-column prop="remark" :show-overflow-tooltip="true" align="center" label="备注" />
      <el-table-column align="center" label="操作">
        <template slot-scope="scope">
          <el-button size="mini" type="text" icon="el-icon-circle-close" @click="handleCancelPerm(scope.row)" v-if="scope.row.userId != 1"
            v-hasPermi="['system:roleusers:del']">取消授权</el-button>
        </template>
      </el-table-column>
    </el-table>
    <pagination :total="dataRoleUserCount" :page.sync="roleUserQueryParams.pageNum" :limit.sync="roleUserQueryParams.pageSize"
      @pagination="getRoleUser" />

    <!-- 添加或修改菜单对话框 -->
    <el-dialog title="添加用户" :visible.sync="open" append-to-body @close="cancel">
      <el-form style="display:flex" :inline="true" @submit.native.prevent>
        <el-form-item style="margin-left:auto">
          <el-input v-model="userQueryParams.userName" placeholder="请输入用户名称" clearable prefix-icon="el-icon-search"
            @keyup.enter.native="handleSearchRoleUser" />
        </el-form-item>
      </el-form>
      <el-row>
        <el-col>
          <el-table ref="userTable" v-loading="loadingUser" :data="dataUserTable" row-key="userId" stripe border :height="tableHeight*0.5">
            <el-table-column type="selection" width="55" align="center" />
            <el-table-column prop="userId" align="center" label="用户编号" width="150" />
            <el-table-column prop="userName" align="center" label="用户名称" width="150" />
            <el-table-column prop="nickName" align="center" label="用户昵称" width="150" />
            <el-table-column prop="status" align="center" label="用户状态">
              <template slot-scope="scope" label="操作">
                <i :style="scope.row.status === '0' ?'color:green':'color:red'"
                  :class="scope.row.status === '0' ? 'el-icon-success ':'el-icon-error'" />
              </template>
            </el-table-column>
          </el-table>
          <pagination :total="dataUserCount" :page.sync="userQueryParams.pageNum" :limit.sync="userQueryParams.pageSize"
            @pagination="handleGetUserTable" />
        </el-col>
      </el-row>
      <div slot="footer" class="dialog-footer">
        <el-button type="primary" @click="handleSubmit">确 定</el-button>
        <el-button @click="open = false">取 消</el-button>
      </div>
    </el-dialog>
  </div>
</template>
<script>
// import { listRole } from "@/api/system/role";
import { getRole } from "@/api/system/role";
import {
  getRoleUsers,
  createRoleUsers,
  deleteRoleUsers,
  getExcludeUsers,
} from "@/api/system/userRoles";
export default {
  name: "roleusers",
  data() {
    return {
      // 遮罩层
      loadingRole: false,
      loadingUser: false,
      loadingRoleUser: false,
      // 表格高度
      tableHeight: window.innerHeight,
      // 角色列表
      dataRoleTable: [],
      // 已添加用户列表
      dataRoleUserTable: [],
      dataRoleUserCount: 0,
      // 未添加用户列表
      dataUserTable: [],
      dataUserCount: 0,
      // 勾选添加用户列表
      addSelections: [],
      // 勾选删除用户列表
      delSelections: [],
      // 过滤查询
      search: "",
      // 角色id
      roleId: "",
      // 是否显示弹出层
      open: false,
      // 角色用户查询参数
      roleUserQueryParams: {
        pageNum: 1,
        pageSize: 10,
        roleId: undefined,
        userName: undefined,
        roleName: undefined,
        roleKey: undefined,
      },
      userQueryParams: {
        pageNum: 1,
        pageSize: 10,
        roleId: undefined,
        userName: undefined,
      },
      // 状态字典
      statusOptions: [],
    };
  },
  created() {
    // 获取角色列表
    // this.loadingRole = true;
    // listRole({ pageSize: 50 }).then((response) => {
    //   this.dataRoleTable = response.data.result;
    //   // this.handleRoleTableSelection(this.dataRoleTable[0]);
    //   // this.$refs.roleTable.setCurrentRow(this.dataRoleTable[0]);
    //   this.loadingRole = false;
    // });
    this.getDicts("sys_normal_disable").then((response) => {
      this.statusOptions = response.data;
    });
    var roleId = this.$route.query.roleId;
    console.log(roleId);
    if (roleId) {
      this.roleId = roleId;
      this.searchRoleUser();

      getRole(roleId).then((response) => {
        const { code, data } = response;
        if (code == 200) {
          this.roleUserQueryParams.roleName = data.roleName;
          this.roleUserQueryParams.roleKey = data.roleKey;
        }
      });
    } else {
      this.msgError("请升级数据库");
    }
  },
  methods: {
    searchRoleUser() {
      this.roleUserQueryParams.pageNum = 1;
      this.getRoleUser();
    },
    // 获取角色用户
    getRoleUser() {
      this.loadingRoleUser = true;
      this.roleUserQueryParams.roleId = this.roleId;
      getRoleUsers(this.roleUserQueryParams).then((response) => {
        this.dataRoleUserTable = response.data.result;
        this.dataRoleUserCount = response.data.totalNum;
        this.loadingRoleUser = false;
      });
    },
    // 返回按钮
    handleClose() {
      const obj = { path: "/system/role" };
      this.$tab.closeOpenPage(obj);
    },
    // 批量删除角色用户
    cancelAuthUserAll() {
      this.delSelections = [];
      this.$refs.roleUserTable.selection.forEach((element) => {
        this.delSelections.push(element.userId);
      });
      if (this.delSelections.length === 0) {
        this.msgError("请选择要删除的用户");
        return;
      }
      this.$confirm(
        "是否确认删除选中的 " + this.delSelections.length + " 条数据?",
        "警告",
        {
          confirmButtonText: "确定",
          cancelButtonText: "取消",
          type: "warning",
        }
      )
        .then(() => {
          deleteRoleUsers({
            roleId: this.roleId,
            userIds: this.delSelections,
          }).then((response) => {
            if (response.code === 200) {
              this.$message({
                message: "成功删除" + response.data + "条数据",
                type: "success",
              });
              this.getRoleUser();
            }
          });
        })
        .catch(() => {});
    },
    // 取消授权
    handleCancelPerm(row) {
      this.delSelections = [];
      this.delSelections.push(row.userId);

      deleteRoleUsers({
        roleId: this.roleId,
        userIds: this.delSelections,
      }).then((response) => {
        if (response.code === 200) {
          this.$message({
            message: "成功删除" + response.data + "条数据",
            type: "success",
          });
          this.getRoleUser();
        }
      });
    },
    // 选中角色
    // handleRoleTableSelection(row) {
    //   this.roleId = row.roleId;
    //   this.getRoleUser();
    // },
    handleSearchRoleUser() {
      this.userQueryParams.pageNum = 1;
      this.handleGetUserTable();
    },
    // 获取未添加角色列表
    handleGetUserTable() {
      this.open = true;
      this.loadingUser = true;
      this.userQueryParams.roleId = this.roleId;
      getExcludeUsers(this.userQueryParams).then((response) => {
        this.dataUserTable = response.data.result;
        this.dataUserCount = response.data.totalNum;
        this.loadingUser = false;
      });
    },
    // 新增用户角色
    handleSubmit() {
      this.addSelections = [];
      this.$refs.userTable.selection.forEach((element) => {
        this.addSelections.push(element.userId);
      });

      createRoleUsers({
        roleId: this.roleId,
        userIds: this.addSelections,
      }).then((response) => {
        if (response.code === 200) {
          this.$message({
            message: "成功添加" + response.data + "条数据",
            type: "success",
          });
          this.getRoleUser();
          this.open = false;
        }
      });
    },
    cancel() {
      this.open = false;
    },
  },
};
</script>
