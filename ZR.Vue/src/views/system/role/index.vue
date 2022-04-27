<template>
  <div class="app-container">
    <el-form :model="queryParams" ref="queryForm" v-show="showSearch" :inline="true">
      <el-form-item label="角色名称" prop="roleName">
        <el-input v-model="queryParams.roleName" placeholder="请输入角色名称" clearable size="small" style="width: 240px"
          @keyup.enter.native="handleQuery" />
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
      <right-toolbar :showSearch.sync="showSearch" @queryTable="getList"></right-toolbar>
    </el-row>

    <el-table v-loading="loading" :data="roleList" highlight-current-row @selection-change="handleSelectionChange">
      <el-table-column label="编号" prop="roleId" width="80" />
      <el-table-column label="名称" prop="roleName" />
      <el-table-column label="显示顺序" prop="roleSort"></el-table-column>
      <el-table-column label="权限字符" prop="roleKey" />
      <el-table-column label="权限范围" prop="dataScope" :formatter="dataScopeFormat"></el-table-column>
      <el-table-column label="状态" align="center" width="90">
        <template slot-scope="scope">
          <el-switch v-model="scope.row.status" :disabled="scope.row.roleKey == 'admin'" active-value="0" inactive-value="1"
            @change="handleStatusChange(scope.row)"></el-switch>
        </template>
      </el-table-column>
      <el-table-column label="用户个数" align="center" prop="userNum" width="90" />
      <el-table-column label="创建时间" align="center" prop="createTime" width="150" />
      <el-table-column label="备注" align="center" prop="remark" width="150" :show-overflow-tooltip="true" />
      <el-table-column label="操作" align="center" width="200">
        <template slot-scope="scope" v-if="scope.row.roleKey != 'admin'">
          <el-button size="mini" type="text" icon="el-icon-edit" @click.stop="handleUpdate(scope.row)" v-hasPermi="['system:role:edit']">修改
          </el-button>
          <el-button size="mini" type="text" icon="el-icon-delete" @click.stop="handleDelete(scope.row)" v-hasPermi="['system:role:remove']">删除
          </el-button>

          <el-dropdown size="mini" @command="(command) => handleCommand(command, scope.row)" v-hasPermi="['system:role:edit']">
            <span class="el-dropdown-link">
              <i class="el-icon-d-arrow-right el-icon--right"></i>更多
            </span>
            <el-dropdown-menu slot="dropdown">
              <el-dropdown-item command="handleDataScope" icon="el-icon-circle-check" v-hasPermi="['system:role:authorize']">菜单权限</el-dropdown-item>
              <el-dropdown-item command="handleAuthUser" icon="el-icon-user" v-hasPermi="['system:roleusers:list']">分配用户</el-dropdown-item>
            </el-dropdown-menu>
          </el-dropdown>
        </template>
      </el-table-column>
    </el-table>
    <pagination :total="total" :page.sync="queryParams.pageNum" :limit.sync="queryParams.pageSize" @pagination="getList" />

    <!-- 角色菜单弹框 -->
    <el-dialog title="角色权限分配" :visible.sync="showRoleScope" width="500px">
      <el-form :model="form" label-width="80px">
        <el-form-item label="菜单搜索">
          <el-input placeholder="请输入关键字进行过滤" v-model="searchText"></el-input>
        </el-form-item>
        <el-form-item label="权限字符">
          {{form.roleKey}}
        </el-form-item>
        <el-form-item label="数据权限">
          <el-checkbox v-model="menuExpand" @change="handleCheckedTreeExpand($event, 'menu')">展开/折叠</el-checkbox>
          <el-checkbox v-model="menuNodeAll" @change="handleCheckedTreeNodeAll($event, 'menu')">全选/全不选</el-checkbox>
          <el-checkbox v-model="form.menuCheckStrictly" @change="handleCheckedTreeConnect($event, 'menu')">父子联动</el-checkbox>
          <el-tree class="tree-border" :data="menuOptions" show-checkbox ref="menu" node-key="id" :check-strictly="!form.menuCheckStrictly"
            empty-text="加载中，请稍后" :filter-node-method="menuFilterNode" :props="defaultProps"></el-tree>
        </el-form-item>
      </el-form>
      <div slot="footer" class="dialog-footer">
        <el-button type="primary" @click="submitDataScope" v-hasPermi="['system:role:authorize']">保存</el-button>
        <el-button @click="cancel">取 消</el-button>
      </div>
    </el-dialog>

    <!-- 添加或修改角色配置对话框 -->
    <el-dialog :title="title" :visible.sync="open" width="600px" append-to-body>
      <el-form ref="form" :model="form" :rules="rules" label-width="80px">
        <el-row>
          <el-col :lg="12">
            <el-form-item label="角色名称" prop="roleName">
              <el-input v-model="form.roleName" placeholder="请输入角色名称" />
            </el-form-item>
          </el-col>
          <el-col :lg="12">
            <el-form-item label="权限字符" prop="roleKey">
              <el-input v-model="form.roleKey" placeholder="请输入权限字符" />
            </el-form-item>
          </el-col>
          <el-col :lg="12">
            <el-form-item label="角色顺序" prop="roleSort">
              <el-input-number v-model="form.roleSort" controls-position="right" :min="0" />
            </el-form-item>
          </el-col>
          <el-col :lg="12">
            <el-form-item label="数据范围">
              <el-select v-model="form.dataScope" @change="dataScopeSelectChange">
                <el-option v-for="item in dataScopeOptions" :key="item.dictValue" :label="item.dictLabel" :value="item.dictValue"></el-option>
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :lg="12">
            <el-form-item label="状态">
              <el-radio-group v-model="form.status">
                <el-radio v-for="dict in statusOptions" :key="dict.dictValue" :label="dict.dictValue">{{ dict.dictLabel }}</el-radio>
              </el-radio-group>
            </el-form-item>
          </el-col>
          <el-col :lg="24">
            <el-form-item label="数据权限" v-show="form.dataScope == 2">
              <el-checkbox v-model="deptExpand" @change="handleCheckedTreeExpand($event, 'dept')">展开/折叠</el-checkbox>
              <el-checkbox v-model="deptNodeAll" @change="handleCheckedTreeNodeAll($event, 'dept')">全选/全不选</el-checkbox>
              <el-checkbox v-model="form.deptCheckStrictly" @change="handleCheckedTreeConnect($event, 'dept')">父子联动</el-checkbox>
              <el-tree class="tree-border" :data="deptOptions" show-checkbox default-expand-all ref="dept" node-key="id"
                :check-strictly="!form.deptCheckStrictly" empty-text="加载中，请稍候" :props="defaultProps"></el-tree>
            </el-form-item>
          </el-col>
          <el-col :lg="24">
            <el-form-item label="备注">
              <el-input v-model="form.remark" type="textarea" placeholder="请输入内容"></el-input>
            </el-form-item>
          </el-col>
        </el-row>
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
  changeRoleStatus
} from '@/api/system/role'
import {
  roleMenuTreeselect
} from '@/api/system/menu'
import {
  treeselect as deptTreeselect,
  roleDeptTreeselect
} from '@/api/system/dept'

export default {
  name: 'role',
  data() {
    return {
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
      title: '',
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
      // 是否显示下拉菜单分配
      showRoleScope: false,
      // 数据范围选项
      dataScopeOptions: [
        {
          dictValue: '1',
          dictLabel: '全部'
        },
        {
          dictValue: "2",
          dictLabel: "自定义",
        },
        {
          dictValue: '3',
          dictLabel: '本部门'
        },
        {
          dictValue: "4",
          dictLabel: "本部门及以下数据权限",
        },
        {
          dictValue: '5',
          dictLabel: '仅本人'
        }
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
        status: undefined
      },
      searchText: '',
      // 表单参数
      form: {},
      defaultProps: {
        children: 'children',
        label: 'label'
      },
      // 表单校验
      rules: {
        roleName: [
          { required: true, message: '角色名称不能为空', trigger: 'blur' }
        ],
        roleKey: [
          { required: true, message: '权限字符不能为空', trigger: 'blur' }
        ],
        roleSort: [
          { required: true, message: '角色顺序不能为空', trigger: 'blur' }
        ]
      }
    }
  },
  watch: {
    searchText(val) {
      this.$refs.menu.filter(val)
    }
  },
  created() {
    this.getList()
    this.getDicts('sys_normal_disable').then((response) => {
      this.statusOptions = response.data
    })
  },
  methods: {
    /** 查询角色列表 */
    getList() {
      this.loading = true

      listRole(this.addDateRange(this.queryParams, this.dateRange)).then(
        (response) => {
          this.roleList = response.data.result
          this.total = response.data.totalNum
          this.loading = false
        }
      )
    },
    /** 查询部门树结构 */
    getDeptTreeselect() {
      deptTreeselect().then((response) => {
        this.deptOptions = response.data
      })
    },
    // 所有菜单节点数据
    getMenuAllCheckedKeys() {
      // 目前被选中的菜单节点
      const checkedKeys = this.$refs.menu.getCheckedKeys()
      // 半选中的菜单节点
      const halfCheckedKeys = this.$refs.menu.getHalfCheckedKeys()
      checkedKeys.unshift.apply(checkedKeys, halfCheckedKeys)
      return checkedKeys
    },
    // 所有部门节点数据
    getDeptAllCheckedKeys() {
      // 目前被选中的部门节点
      const checkedKeys = this.$refs.dept.getCheckedKeys()
      // 半选中的部门节点
      const halfCheckedKeys = this.$refs.dept.getHalfCheckedKeys()
      checkedKeys.unshift.apply(checkedKeys, halfCheckedKeys)
      return checkedKeys
    },
    /** 根据角色ID查询菜单树结构 */
    getRoleMenuTreeselect(roleId) {
      return roleMenuTreeselect(roleId).then((response) => {
        this.menuOptions = response.data.menus
        return response
      })
    },
    /** 根据角色ID查询部门树结构 */
    getRoleDeptTreeselect(roleId) {
      return roleDeptTreeselect(roleId).then((response) => {
        console.log('角色', response)
        this.deptOptions = response.data.depts
        return response
      })
    },
    // 角色状态修改
    handleStatusChange(row) {
      const text = row.status === '0' ? '启用' : '停用'

      console.log(JSON.stringify(row), text)
      this.$confirm(
        '确认要"' + text + '""' + row.roleName + '"角色吗?',
        '警告',
        {
          confirmButtonText: '确定',
          cancelButtonText: '取消',
          type: 'warning'
        }
      )
        .then(function() {
          return changeRoleStatus(row.roleId, row.status)
        })
        .then(() => {
          this.msgSuccess(text + '成功')
        })
        .catch(function() {
          row.status = row.status === '0' ? '1' : '0'
        })
    },
    // 取消按钮
    cancel() {
      this.open = false
      this.showRoleScope = false
      this.reset()
    },
    // 表单重置
    reset() {
      if (this.$refs.menu != undefined) {
        this.$refs.menu.setCheckedKeys([])
      }
      (this.menuExpand = false),
      (this.menuNodeAll = false),
      (this.deptExpand = true),
      (this.deptNodeAll = false),
      (this.form = {
        roleId: undefined,
        roleName: undefined,
        roleKey: undefined,
        roleSort: 99,
        status: '0',
        menuIds: [],
        deptIds: [],
        dataScope: '1',
        menuCheckStrictly: true,
        deptCheckStrictly: true,
        remark: undefined
      })
      this.resetForm('form')
    },
    /** 搜索按钮操作 */
    handleQuery() {
      this.queryParams.pageNum = 1
      this.getList()
    },
    /** 重置按钮操作 */
    resetQuery() {
      this.dateRange = []
      this.resetForm('queryForm')
      this.handleQuery()
    },
    // 多选框选中数据
    handleSelectionChange(selection) {
      this.ids = selection.map((item) => item.roleId)
      this.single = selection.length != 1
      this.multiple = !selection.length
    },
    // 更多操作触发
    handleCommand(command, row) {
      switch (command) {
        case 'handleDataScope':
          this.handleDataScope(row)
          break
        case 'handleAuthUser':
          this.handleAuthUser(row)
          break
        default:
          break
      }
    },
    // 树权限（展开/折叠）
    handleCheckedTreeExpand(value, type) {
      if (type == 'menu') {
        const treeList = this.menuOptions
        for (let i = 0; i < treeList.length; i++) {
          this.$refs.menu.store.nodesMap[treeList[i].id].expanded = value
        }
      } else if (type == 'dept') {
        const treeList = this.deptOptions
        for (let i = 0; i < treeList.length; i++) {
          this.$refs.dept.store.nodesMap[treeList[i].id].expanded = value
        }
      }
    },
    // 树权限（全选/全不选）
    handleCheckedTreeNodeAll(value, type) {
      if (type == 'menu') {
        this.$refs.menu.setCheckedNodes(value ? this.menuOptions : [])
      } else if (type == 'dept') {
        this.$refs.dept.setCheckedNodes(value ? this.deptOptions : [])
      }
    },
    // 树权限（父子联动）
    handleCheckedTreeConnect(value, type) {
      if (type == 'menu') {
        this.form.menuCheckStrictly = !!value
      } else if (type == 'dept') {
        this.form.deptCheckStrictly = !!value
      }
    },
    // 菜单筛选
    menuFilterNode(value, data) {
      if (!value) return true
      return data.label.indexOf(value) !== -1
    },
    /** 新增按钮操作 */
    handleAdd() {
      this.reset()
      this.getDeptTreeselect()
      this.open = true
      this.title = '添加角色'
      this.showRoleScope = false
    },
    /** 修改按钮操作 ok */
    handleUpdate(row) {
      this.reset()
      this.showRoleScope = false
      const roleId = row.roleId || this.ids
      const roleDeptTreeselect = this.getRoleDeptTreeselect(row.roleId)
      getRole(roleId).then((response) => {
        this.form = response.data
        this.open = true
        this.title = '修改角色'

        this.$nextTick(() => {
          roleDeptTreeselect.then((res) => {
            this.$refs.dept.setCheckedKeys(res.data.checkedKeys)
          })
        })
      })
    },
    /** 选择角色权限范围触发 */
    dataScopeSelectChange(value) {
      if (value !== '2') {
        this.$refs.dept.setCheckedKeys([])
      }
    },
    // 数据权限
    dataScopeFormat(row, column) {
      return this.selectDictLabel(this.dataScopeOptions, row.dataScope)
    },
    /** 分配角色权限按钮操作 */
    // 新增 和上面代码基本相同
    handleDataScope(row) {
      if (row.roleId == 1) {
        this.showRoleScope = false
        return
      }
      this.reset()
      this.showRoleScope = true
      const roleId = row.roleId || this.ids
      const roleMenu = this.getRoleMenuTreeselect(roleId)

      roleMenu.then((res) => {
        const checkedKeys = res.data.checkedKeys
        checkedKeys.forEach((v) => {
          this.$nextTick(() => {
            this.$refs.menu.setChecked(v, true, false)
          })
        })
      })
      this.form = {
        roleId: row.roleId,
        roleName: row.roleName,
        roleKey: row.roleKey,
        menuCheckStrictly: row.menuCheckStrictly
      }
    },
    /** 分配用户操作 */
    handleAuthUser: function(row) {
      const roleId = row.roleId
      this.$router.push({ path: '/system/roleusers', query: { roleId }})
    },
    /** 提交按钮 */
    submitForm: function() {
      this.$refs['form'].validate((valid) => {
        if (valid) {
          if (this.form.roleId != undefined && this.form.roleId > 0) {
            this.form.type = 'edit'
            this.form.deptIds = this.getDeptAllCheckedKeys()
            updateRole(this.form).then((response) => {
              this.msgSuccess('修改成功')
              this.open = false
              this.getList()
            })
          } else {
            this.form.type = 'add'
            this.form.deptIds = this.getDeptAllCheckedKeys()
            addRole(this.form).then((response) => {
              console.log(response)
              this.open = false
              if (response.code == 200) {
                this.msgSuccess('新增成功')
                this.getList()
              } else {
                this.msgInfo(response.msg)
              }
            })
          }
        }
      })
    },
    /** 提交按钮（菜单数据权限） */
    submitDataScope: function() {
      if (this.form.roleId != undefined) {
        this.form.menuIds = this.getMenuAllCheckedKeys()
        dataScope(this.form).then((response) => {
          this.msgSuccess('修改成功')
          this.getList()
          this.cancel()
        })
      } else {
        this.msgError('请选择角色')
      }
    },
    /** 删除按钮操作 */
    handleDelete(row) {
      const roleIds = row.roleId || this.ids
      this.$confirm(
        '是否确认删除角色编号为"' + roleIds + '"的数据项?',
        '警告',
        {
          confirmButtonText: '确定',
          cancelButtonText: '取消',
          type: 'warning'
        }
      )
        .then(function() {
          return delRole(roleIds)
        })
        .then(() => {
          this.getList()
          this.msgSuccess('删除成功')
        })
    },
    /** 导出按钮操作 */
    handleExport() {
      const queryParams = this.queryParams
      this.$confirm('是否确认导出所有角色数据项?', '警告', {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning'
      })
        .then(function() {
          return exportRole(queryParams)
        })
        .then((response) => {
          this.download(response.data.path)
        })
    }
  }
}
</script>
<style scoped>
/* tree border */
.tree-border {
  margin-top: 5px;
  border: 1px solid #e5e6e7;
  background: #ffffff none;
  border-radius: 4px;
}
</style>