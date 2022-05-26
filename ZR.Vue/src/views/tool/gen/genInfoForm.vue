<template>
  <el-form ref="genInfoForm" :model="info" :rules="rules" label-width="150px">
    <el-row>
      <el-col :lg="12">
        <el-form-item prop="tplCategory">
          <span slot="label">生成模板</span>
          <el-select v-model="info.tplCategory" @change="tplSelectChange">
            <el-option label="单表（增删改查）" value="crud" />
            <el-option label="单表查询" value="select" />
            <el-option label="树表（增删改查）" value="tree" />
            <el-option label="导航查询(1对1)" value="subNav"></el-option>
            <el-option label="导航查询(1对多)" value="subNavMore"></el-option>
            <!-- <el-option label="主子表（增删改查）" value="sub" /> -->
          </el-select>
        </el-form-item>
      </el-col>

      <el-col :lg="12">
        <el-form-item prop="baseNameSpace">
          <span slot="label">
            生成命名空间前缀
            <el-tooltip content="比如 ZR." placement="top">
              <i class="el-icon-question"></i>
            </el-tooltip>
          </span>
          <el-input v-model="info.baseNameSpace" />
        </el-form-item>
      </el-col>

      <el-col :lg="12">
        <el-form-item prop="moduleName">
          <span slot="label">
            生成模块名
            <el-tooltip content="可理解为子系统名，例如 system、user、tool（一般文件夹归类）" placement="top">
              <i class="el-icon-question"></i>
            </el-tooltip>
          </span>
          <el-input v-model="info.moduleName" auto-complete="" />
        </el-form-item>
      </el-col>

      <el-col :lg="12">
        <el-form-item prop="businessName">
          <span slot="label">
            生成业务名
            <el-tooltip content="可理解为功能英文名，例如 user" placement="top">
              <i class="el-icon-question"></i>
            </el-tooltip>
          </span>
          <el-input v-model="info.businessName" />
        </el-form-item>
      </el-col>

      <el-col :lg="12">
        <el-form-item prop="functionName">
          <span slot="label">
            生成功能名
            <el-tooltip content="用作类描述，例如 用户,代码生成,文章系统" placement="top">
              <i class="el-icon-question"></i>
            </el-tooltip>
          </span>
          <el-input v-model="info.functionName" />
        </el-form-item>
      </el-col>

      <el-col :lg="12">
        <el-form-item>
          <span slot="label">
            上级菜单
            <el-tooltip content="分配到指定菜单下，例如 系统管理" placement="top">
              <i class="el-icon-question"></i>
            </el-tooltip>
          </span>
          <el-cascader
            class="w100"
            :options="menuOptions"
            :props="{ checkStrictly: true, value: 'menuId', label: 'menuName', emitPath: false }"
            placeholder="请选择上级菜单"
            clearable
            v-model="info.parentMenuId"
          >
            <template #default="{ node, data }">
              <span>{{ data.menuName }}</span>
              <span v-if="!node.isLeaf"> ({{ data.children.length }}) </span>
            </template>
          </el-cascader>
        </el-form-item>
      </el-col>
      <el-col :lg="12">
        <el-form-item label="查询排序字段">
          <el-select v-model="info.sortField" placeholder="请选择字段" class="mr10" clearable="">
            <el-option v-for="item in columns" :key="item.columnId" :label="item.csharpField" :value="item.csharpField"> </el-option>
          </el-select>

          <el-radio v-model="info.sortType" label="asc">正序</el-radio>
          <el-radio v-model="info.sortType" label="desc">倒序</el-radio>
        </el-form-item>
      </el-col>
      <el-col :lg="12">
        <el-form-item prop="permissionPrefix">
          <span slot="label">
            权限前缀
            <el-tooltip content="eg：system:user:add中的'system:user'" placement="top">
              <i class="el-icon-question"></i>
            </el-tooltip>
          </span>
          <el-input v-model="info.permissionPrefix" placeholder="请输入权限前缀"></el-input>
        </el-form-item>
      </el-col>
      <el-col :lg="12">
        <el-form-item prop="genType">
          <span slot="label">
            生成代码方式
            <el-tooltip content="默认为zip压缩包下载" placement="top">
              <i class="el-icon-question"></i>
            </el-tooltip>
          </span>
          <el-radio v-model="info.genType" label="0">zip压缩包</el-radio>
          <el-radio v-model="info.genType" label="1">自定义路径</el-radio>
        </el-form-item>
      </el-col>

      <el-col :lg="24" v-if="info.genType == '1'">
        <el-form-item prop="genPath">
          <span slot="label">
            自定义路径
            <el-tooltip content="填写磁盘绝对路径，若不填写，则生成到当前Web项目下" placement="top">
              <i class="el-icon-question"></i>
            </el-tooltip>
          </span>
          <el-input v-model="info.genPath">
            <el-dropdown slot="append">
              <el-button type="primary">
                最近路径快速选择
                <i class="el-icon-arrow-down el-icon--right"></i>
              </el-button>
              <el-dropdown-menu slot="dropdown">
                <el-dropdown-item @click.native="info.genPath = '/'">恢复默认的生成基础路径</el-dropdown-item>
                <el-dropdown-item @click.native="info.genPath = ''">本项目</el-dropdown-item>
              </el-dropdown-menu>
            </el-dropdown>
          </el-input>
        </el-form-item>
      </el-col>
      <el-col :lg="24" v-show="info.tplCategory != 'select'">
        <el-form-item label="显示按钮">
          <el-checkbox-group v-model="checkedBtn" @change="checkedBtnSelect">
            <el-checkbox :label="1">
              <el-tag type="primary">添加</el-tag>
            </el-checkbox>
            <el-checkbox :label="2">
              <el-tag type="success">修改</el-tag>
            </el-checkbox>
            <el-checkbox :label="3">
              <el-tag type="danger">删除</el-tag>
            </el-checkbox>
            <el-checkbox :label="4">
              <el-tag type="warning">导出</el-tag>
            </el-checkbox>
          </el-checkbox-group>
        </el-form-item>
      </el-col>
    </el-row>

    <el-row v-show="info.tplCategory == 'tree'">
      <h4 class="form-header">其他信息</h4>
      <el-col :lg="12">
        <el-form-item>
          <span slot="label">
            树编码字段
            <el-tooltip content="树显示的编码字段名， 如：dept_id" placement="top">
              <i class="el-icon-question"></i>
            </el-tooltip>
          </span>
          <el-select v-model="info.treeCode" placeholder="请选择">
            <el-option
              v-for="(column, index) in columns"
              :key="index"
              :label="column.csharpField + '：' + column.columnComment"
              :value="column.csharpField"
            ></el-option>
          </el-select>
        </el-form-item>
      </el-col>
      <el-col :lg="12">
        <el-form-item>
          <span slot="label">
            树父编码字段
            <el-tooltip content="树显示的父编码字段名， 如：parent_Id" placement="top">
              <i class="el-icon-question"></i>
            </el-tooltip>
          </span>
          <el-select v-model="info.treeParentCode" placeholder="请选择">
            <el-option
              v-for="(column, index) in columns"
              :key="index"
              :label="column.csharpField + '：' + column.columnComment"
              :value="column.csharpField"
            ></el-option>
          </el-select>
        </el-form-item>
      </el-col>
      <el-col :lg="12">
        <el-form-item>
          <span slot="label">
            树名称字段
            <el-tooltip content="树节点的显示名称字段名， 如：dept_name" placement="top">
              <i class="el-icon-question"></i>
            </el-tooltip>
          </span>
          <el-select v-model="info.treeName" placeholder="请选择">
            <el-option
              v-for="(column, index) in columns"
              :key="index"
              :label="column.csharpField + '：' + column.columnComment"
              :value="column.csharpField"
            ></el-option>
          </el-select>
        </el-form-item>
      </el-col>
    </el-row>
    <el-row v-show="info.tplCategory == 'sub' || info.tplCategory == 'subNav' || info.tplCategory == 'subNavMore'">
      <h4 class="form-header">关联信息</h4>
      <el-col :lg="12">
        <el-form-item>
          <span slot="label">
            关联子表的表名
            <el-tooltip content="关联子表的表名， 如：sys_user" placement="top">
              <i class="el-icon-question"></i>
            </el-tooltip>
          </span>
          <el-select v-model="info.subTableName" placeholder="请选择" @change="subSelectChange(this)">
            <el-option v-for="(table, index) in tables" :key="index" :label="table.tableName + '：' + table.tableComment" :value="table.tableName">
            </el-option>
          </el-select>
        </el-form-item>
      </el-col>
      <el-col :lg="12">
        <el-form-item>
          <span slot="label">
            子表关联的外键名
            <el-tooltip content="子表关联的外键名， 如：user_id" placement="top">
              <i class="el-icon-question"></i>
            </el-tooltip>
          </span>
          <el-select v-model="info.subTableFkName" placeholder="请选择">
            <el-option v-for="(column, index) in subColumns" :key="index" :label="column.csharpField" :value="column.csharpField">
              <span style="float: left">{{ column.csharpField }}</span>
              <span style="float: right">{{ column.columnComment }}</span>
            </el-option>
          </el-select>
        </el-form-item>
      </el-col>
    </el-row>
  </el-form>
</template>
<script>
import { queryColumnInfo } from '@/api/tool/gen'
import { listMenu } from '@/api/system/menu'

export default {
  name: 'BasicInfoForm',
  props: {
    info: {
      type: Object,
      default: null,
    },
    // 子表
    tables: {
      type: Array,
      default: null,
    },
    menus: {
      type: Array,
      default: [],
    },
    // 列
    columns: {
      type: Array,
      default: [],
    },
  },
  data() {
    return {
      menuOptions: [],
      checkedBtn: [],
      subColumns: [],
      rules: {
        tplCategory: [{ required: true, message: '请选择生成模板', trigger: 'blur' }],
        moduleName: [
          {
            required: true,
            message: '请输入生成模块名',
            trigger: 'blur',
            pattern: /^[A-Za-z]+$/,
          },
        ],
        businessName: [
          {
            required: true,
            message: '请输入生成业务名',
            trigger: 'blur',
            pattern: /^[A-Za-z]+$/,
          },
        ],
        functionName: [{ required: true, message: '请输入生成功能名', trigger: 'blur' }],
        permissionPrefix: {
          required: true,
          message: '请输入权限前缀',
          trigger: 'blur',
        },
      },
    }
  },
  watch: {
    'info.subTableName': function (val) {
      this.setSubTableColumns(val)
    },
    'info.checkedBtn': function (val) {
      this.checkedBtn = val
    },
  },
  methods: {
    /** 转换菜单数据结构 */
    normalizer(node) {
      if (node.children && !node.children.length) {
        delete node.children
      }
      return {
        id: node.menuId,
        label: node.menuName,
        children: node.children,
      }
    },
    /** 选择子表名触发 */
    subSelectChange(value) {
      this.info.subTableFkName = ''
    },
    checkedBtnSelect(value) {
      this.info.checkedBtn = value
    },
    /** 选择生成模板触发 */
    tplSelectChange(value) {
      if (value !== 'sub') {
        this.info.subTableName = ''
        this.info.subTableFkName = ''
      }
    },
    /** 设置关联外键 */
    setSubTableColumns(value) {
      if (value == null || value == undefined || value == '') {
        return
      }
      for (var item in this.tables) {
        const obj = this.tables[item]
        if (value === obj.tableName) {
          queryColumnInfo(obj.tableId).then((res) => {
            if (res.code == 200) {
              this.subColumns = res.data.columns
            }
          })
          break
        }
      }
    },
    /** 查询菜单下拉树结构 */
    getMenuTreeselect() {
      /** 查询菜单下拉列表 */
      listMenu({ menuTypeIds: 'M,C' }).then((response) => {
        this.menuOptions = response.data
      })
    },
  },
  mounted() {
    this.getMenuTreeselect()
  },
}
</script>
