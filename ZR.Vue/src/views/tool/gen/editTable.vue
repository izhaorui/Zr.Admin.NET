<template>
  <el-card>
    <el-tabs v-model="activeName" tab-position="top">
      <el-tab-pane label="基本信息" name="basic">
        <basic-info-form ref="basicInfo" :info="info" />
      </el-tab-pane>
      <el-tab-pane label="生成信息" name="genInfo">
        <gen-info-form ref="genInfo" :info="info" :tables="tables" :menus="menus" :columns="columns" />
      </el-tab-pane>
      <el-tab-pane label="字段信息" name="cloum">
        <el-table ref="dragTable" v-loading="loading" :data="columns" row-key="columnId" min-height="150px" :max-height="tableHeight">
          <el-table-column label="序号" type="index" class-name="allowDrag"  />
          <el-table-column label="字段列名" prop="columnName" :show-overflow-tooltip="true"  />
          <el-table-column label="字段描述" >
            <template slot-scope="scope">
              <el-input v-model="scope.row.columnComment" :ref="scope.row.columnId" @keydown.native="nextFocus(scope.row, scope.$index, $event)">
              </el-input>
            </template>
          </el-table-column>
          <el-table-column label="物理类型" prop="columnType" :show-overflow-tooltip="true" />
          <el-table-column label="C#类型">
            <template slot-scope="scope">
              <el-select v-model="scope.row.csharpType">
                <el-option label="int" value="int" />
                <el-option label="long" value="long" />
                <el-option label="string" value="string" />
                <el-option label="double" value="double" />
                <el-option label="decimal" value="decimal" />
                <el-option label="DateTime" value="DateTime" />
                <el-option label="bool" value="bool" />
              </el-select>
            </template>
          </el-table-column>
          <el-table-column label="C#属性">
            <template slot-scope="scope">
              <el-input v-model="scope.row.csharpField"></el-input>
            </template>
          </el-table-column>
          <el-table-column label="插入" width="50" align="center" v-if="info.tplCategory != 'select'">
            <template slot-scope="scope">
              <el-checkbox v-model="scope.row.isInsert" :disabled="scope.row.isIncrement"></el-checkbox>
            </template>
          </el-table-column>
          <el-table-column label="编辑" width="50" align="center" v-if="info.tplCategory != 'select'">
            <template slot-scope="scope">
              <el-checkbox v-model="scope.row.isEdit" :disabled="scope.row.isPk || scope.row.isIncrement"></el-checkbox>
            </template>
          </el-table-column>
          <el-table-column label="列表" width="50" align="center">
            <template slot-scope="scope">
              <el-checkbox v-model="scope.row.isList"></el-checkbox>
            </template>
          </el-table-column>
          <el-table-column label="查询" width="50" align="center">
            <template slot-scope="scope">
              <el-checkbox v-model="scope.row.isQuery" :disabled="scope.row.htmlType == 'imageUpload' || scope.row.htmlType == 'fileUpload'">
              </el-checkbox>
            </template>
          </el-table-column>
          <el-table-column label="查询方式">
            <template slot-scope="scope">
              <el-select v-model="scope.row.queryType" :disabled="scope.row.htmlType == 'datetime'" v-if="scope.row.isQuery">
                <el-option label="=" value="EQ" />
                <el-option label="!=" value="NE" />
                <el-option label=">" value="GT" />
                <el-option label=">=" value="GTE" />
                <el-option label="<" value="LT" />
                <el-option label="<=" value="LTE" />
                <el-option label="LIKE" value="LIKE" />
                <el-option label="BETWEEN" value="BETWEEN" />
              </el-select>
            </template>
          </el-table-column>
          <el-table-column label="必填" width="50" align="center">
            <template slot-scope="scope">
              <el-checkbox v-model="scope.row.isRequired"></el-checkbox>
            </template>
          </el-table-column>
          <el-table-column label="表单显示类型" width="120">
            <template slot-scope="scope">
              <el-select v-model="scope.row.htmlType">
                <el-option label="文本框" value="input" />
                <el-option label="数字框" value="inputNumber" />
                <el-option label="文本域" value="textarea" />
                <el-option label="下拉框" value="select" />
                <el-option label="单选框" value="radio" />
                <el-option label="复选框" value="checkbox" />
                <el-option label="日期控件" value="datetime" />
                <el-option label="图片上传" value="imageUpload" />
                <el-option label="文件上传" value="fileUpload" />
                <el-option label="富文本控件" value="editor" />
                <el-option label="自定义输入框" value="customInput" />
              </el-select>
            </template>
          </el-table-column>
          <el-table-column label="字典类型" min-width="100">
            <template slot-scope="scope">
              <el-select
                v-model="scope.row.dictType"
                clearable
                filterable
                placeholder="请选择"
                v-if="scope.row.htmlType == 'select' || scope.row.htmlType == 'radio' || scope.row.htmlType == 'checkbox'"
              >
                <el-option v-for="dict in dictOptions" :key="dict.dictType" :label="dict.dictName" :value="dict.dictType">
                  <span style="float: left">{{ dict.dictName }}</span>
                  <span style="float: right; color: #8492a6; font-size: 13px">{{ dict.dictType }}</span>
                </el-option>
              </el-select>
            </template>
          </el-table-column>
          <el-table-column label="备注" align="center" width="200">
            <template slot-scope="scope">
              <el-input v-model="scope.row.remark"> </el-input>
            </template>
          </el-table-column>
        </el-table>
      </el-tab-pane>
    </el-tabs>
    <el-form label-width="100px">
      <el-form-item style="text-align: center; margin-left: -100px; margin-top: 10px">
        <el-button type="primary" icon="el-icon-check" @click="submitForm()">提交</el-button>
        <el-button type="success" icon="el-icon-refresh" @click="handleQuery()">刷新</el-button>
        <el-button icon="el-icon-back" @click="close()">返回</el-button>
      </el-form-item>
    </el-form>
  </el-card>
</template>
<script>
import { updateGenTable, getGenTable } from '@/api/tool/gen'
import { listType } from '@/api/system/dict/type'
import { listMenu as getMenuTreeselect } from '@/api/system/menu'
import basicInfoForm from './basicInfoForm'
import genInfoForm from './genInfoForm'
import Sortable from 'sortablejs'

export default {
  name: 'genedit',
  components: {
    basicInfoForm,
    genInfoForm,
  },
  data() {
    return {
      // 选中选项卡的 name
      activeName: 'cloum',
      // 表格的高度
      tableHeight: document.documentElement.scrollHeight - 245 + 'px',
      // 表信息
      tables: [],
      // 表列信息
      columns: [],
      // 字典信息
      dictOptions: [],
      // 菜单信息
      menus: [],
      // 表详细信息
      info: {},
      loading: true,
    }
  },
  created() {
    this.handleQuery()
  },
  methods: {
    handleQuery() {
      const tableId = this.$route.query && this.$route.query.tableId

      if (tableId) {
        // 获取表详细信息
        getGenTable(tableId).then((res) => {
          this.loading = false
          this.columns = res.data.info.columns
          this.info = { ...res.data.info, ...res.data.info.options }
          this.tables = res.data.tables // 子表
        })
        /** 查询字典下拉列表 */
        listType({ pageSize: 100 }).then((response) => {
          this.dictOptions = response.data.result
        })
        /** 查询菜单下拉列表 */
        getMenuTreeselect().then((response) => {
          this.menus = this.handleTree(response.data, 'menuId')
        })
      }
    },
    /** 提交按钮 */
    submitForm() {
      const basicForm = this.$refs.basicInfo.$refs.basicInfoForm
      const genForm = this.$refs.genInfo.$refs.genInfoForm
      Promise.all([basicForm, genForm].map(this.getFormPromise)).then((res) => {
        const validateResult = res.every((item) => !!item)
        if (validateResult) {
          const genTable = Object.assign({}, basicForm.model, genForm.model)
          genTable.columns = this.columns
          // 额外参数拼接
          genTable.params = {
            treeCode: genTable.treeCode,
            treeName: genTable.treeName,
            treeParentCode: genTable.treeParentCode,
            parentMenuId: genTable.parentMenuId,
            sortField: genTable.sortField,
            sortType: genTable.sortType,
            checkedBtn: genTable.checkedBtn,
            permissionPrefix: genTable.permissionPrefix,
          }
          console.log('genForm', genTable)

          updateGenTable(genTable).then((res) => {
            this.msgSuccess(res.msg)
            if (res.code === 200) {
              this.close()
            }
          })
        } else {
          this.msgError('表单校验未通过，请重新检查提交内容')
        }
      })
    },
    getFormPromise(form) {
      return new Promise((resolve) => {
        form.validate((res) => {
          resolve(res)
        })
      })
    },
    /** 关闭按钮 */
    close() {
      const obj = {
        path: '/tool/gen',
        query: { t: Date.now(), pageNum: this.$route.query.pageNum },
      }
      this.$tab.closeOpenPage(obj)
    },
    /**
     * 排序保存
     */
    sortTable(columns) {
      const el = this.$refs.dragTable.$el.querySelectorAll('.el-table__body-wrapper > table > tbody')[0]
      var that = this
      const sortable = Sortable.create(el, {
        handle: '.allowDrag',
        onEnd: (evt) => {
          const targetRow = that.columns.splice(evt.oldIndex, 1)[0]
          columns.splice(evt.newIndex, 0, targetRow)
          for (const index in columns) {
            columns[index].sort = parseInt(index) + 1
          }
          this.$nextTick(() => {
            this.columns = columns
          })
        },
      })
    },
    /**
     * 回车到下一行
     */
    nextFocus(row, index, e) {
      // const val = e.target.value;
      var keyCode = e.keyCode || e.which || e.charCode
      if (keyCode === 13) {
        this.$refs[row.columnId].blur()
        if (Object.keys(this.$refs).length - 1 === index) {
          index = -1
        }
        var num = Object.keys(this.$refs)[index + 1]
        if (num > 0) {
          this.$refs[num].focus()
        } else {
          console.warn('最后一行了')
        }
      }
    },
  },
  watch: {
    columns: {
      handler(val) {
        this.sortTable(val)
      },
    },
  },
}
</script>
