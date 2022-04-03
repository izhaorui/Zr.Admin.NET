<template>
  <div class="app-container">
    <el-row v-if="searchToggle" :gutter="20">
      <el-col>
        <el-form :inline="true" @submit.native.prevent>
          <el-form-item>
            <el-input v-model="queryParams.queryText" placeholder="请输入计划任务名称" clearable prefix-icon="el-icon-search" @keyup.enter.native="handleQuery"
              @clear="handleQuery" />
          </el-form-item>
          <el-form-item>
            <el-button type="primary" icon="el-icon-search" @click="handleQuery">搜索</el-button>
            <el-button icon="el-icon-refresh" @click="handleReset">重置</el-button>
          </el-form-item>
        </el-form>
      </el-col>
    </el-row>
    <el-row class="mb8" :gutter="20">
      <el-col :span="1.5">
        <el-button v-hasPermi="['monitor:job:add']" plain type="primary" icon="el-icon-plus" size="mini" @click="handleCreate">新增</el-button>
      </el-col>
      <!-- <el-col :span="1.5">
        <el-button v-hasPermi="['monitor:job:edit']" plain type="success" icon="el-icon-edit" size="mini" @click="handleRun(null)" :disabled="single">运行一次</el-button>
      </el-col>
      <el-col :span="1.5">
        <el-button v-hasPermi="['monitor:job:delete']" plain type="danger" icon="el-icon-remove" size="mini" @click="handleDelete(null)" :disabled="single">删除</el-button>
      </el-col>-->
      <el-col :span="1.5">
        <el-button type="warning" plain icon="el-icon-download" size="mini" @click="handleExport" v-hasPermi="['monitor:job:export']">导出</el-button>
      </el-col>
      <el-col :span="1.5">
        <el-button plain v-hasPermi="['monitor:job:query']" type="info" icon="el-icon-s-operation" size="mini" @click="handleJobLog()">日志
        </el-button>
      </el-col>
      <right-toolbar :showSearch.sync="searchToggle" @queryTable="handleQuery"></right-toolbar>
    </el-row>
    <el-row>
      <el-table ref="tasks" v-loading="loading" :data="dataTasks" border="" row-key="id" @sort-change="handleSortable">
        <el-table-column type="index" :index="handleIndexCalc" label="#" align="center" />
        <el-table-column prop="name" :show-overflow-tooltip="true" label="任务名称" width="80" />
        <el-table-column prop="jobGroup" :show-overflow-tooltip="true" align="center" label="任务分组" width="80" />
        <el-table-column prop="assemblyName" align="center" label="程序集名称" :show-overflow-tooltip="true" />
        <el-table-column prop="className" align="center" label="任务类名" :show-overflow-tooltip="true" />
        <el-table-column prop="runTimes" align="center" label="运行次数" width="80" />
        <el-table-column prop="intervalSecond" align="center" label="执行间隔(s)" width="90" />
        <el-table-column prop="cron" align="center" label="运行表达式" :show-overflow-tooltip="true" />
        <el-table-column sortable prop="isStart" align="center" label="任务状态" width="100">
          <template slot-scope="scope">
            <el-tag size="mini" :type="scope.row.isStart ? 'success' : 'danger'">{{ scope.row.isStart ? "运行中":"已停止" }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="lastRunTime" align="center" label="最后运行时间" :show-overflow-tooltip="true" />
        <el-table-column prop="remark" align="center" label="备注" :show-overflow-tooltip="true" />
        <el-table-column label="操作" align="center" width="230" class-name="small-padding fixed-width">
          <template slot-scope="scope">
            <el-button type="text" size="mini" icon="el-icon-view" v-hasPermi="['monitor:job:query']"
              @click="handleJobLog(scope.row.id, scope.row.name)">
              日志
              <!-- <router-link :to="{path: 'job/log', query: {jobId: scope.row.id}}">日志</router-link> -->
            </el-button>
            <el-button type="text" v-if="scope.row.isStart" v-hasPermi="['monitor:job:run']" size="mini" icon="el-icon-remove" title="运行"
              @click="handleRun(scope.row)">运行</el-button>
            <el-button type="text" v-if="scope.row.isStart" v-hasPermi="['monitor:job:stop']" size="mini" icon="el-icon-video-pause" style="color:red"
              title="停止" @click="handleStop(scope.row)">停止</el-button>

            <el-button type="text" v-if="!scope.row.isStart" v-hasPermi="['monitor:job:start']" size="mini" icon="el-icon-video-play" title="启动"
              @click="handleStart(scope.row)">启动</el-button>
            <el-button type="text" v-if="!scope.row.isStart" v-hasPermi="['monitor:job:edit']" size="mini" icon="el-icon-edit" style="color:gray"
              title="编辑" @click="handleUpdate(scope.row)">编辑</el-button>
            <el-button type="text" v-if="!scope.row.isStart" v-hasPermi="['monitor:job:delete']" size="mini" icon="el-icon-delete" style="color:red"
              title="删除" @click="handleDelete(scope.row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
      <pagination :total="total" :page.sync="queryParams.PageNum" :limit.sync="queryParams.pageSize" @pagination="getList" />
    </el-row>

    <el-dialog :title="title" :visible.sync="open" width="600px" append-to-body>
      <el-form ref="form" :model="form" :rules="rules" label-width="100px">
        <el-row>
          <el-col :lg="24" v-if="this.form.id">
            <el-form-item label="任务ID">
              <div>{{form.id}}</div>
            </el-form-item>
          </el-col>
          <el-col :lg="12">
            <el-form-item label="任务名称" maxlength="200" prop="name">
              <el-input v-model="form.name" placeholder="请输入任务名称" />
            </el-form-item>
          </el-col>
          <el-col :lg="12">
            <el-form-item label="触发器类型" prop="triggerType">
              <el-select v-model="form.triggerType" placeholder="请选择触发器类型" style="width:100%">
                <el-option v-for="item in triggerTypeOptions" :key="item.value" :label="item.label" :value="parseInt(item.value)" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :lg="12">
            <el-form-item label="任务分组" maxlength="200" prop="jobGroup">
              <el-select v-model="form.jobGroup" placeholder="请选择">
                <el-option v-for="dict in jobGroupOptions" :key="dict.dictValue" :label="dict.dictLabel" :value="dict.dictValue"></el-option>
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :lg="24">
            <el-form-item label="任务类型" prop="taskType">
              <el-radio-group v-model="form.taskType">
                <el-radio :label="1">执行程序集</el-radio>
                <el-radio :label="2">执行url</el-radio>
              </el-radio-group>
            </el-form-item>
          </el-col>

          <el-col :lg="24" v-if="form.taskType == 2">
            <el-form-item label="apiUrl" prop="apiUrl">
              <el-input v-model="form.apiUrl" placeholder="远程调用接口url">
                <template slot="prepend">Http://</template>
              </el-input>
            </el-form-item>
          </el-col>
          <template v-else>
            <el-col :lg="24">
              <el-form-item label="程序集名称" maxlength="200" prop="assemblyName">
                <el-input v-model="form.assemblyName" placeholder="请输入程序集名称" />
              </el-form-item>
            </el-col>
            <el-col :lg="24">
              <el-form-item label="任务类名" maxlength="200" prop="className">
                <el-input v-model="form.className" placeholder="请输入任务类名" />
              </el-form-item>
            </el-col>
          </template>

          <el-col :lg="24">
            <el-form-item label="传入参数" prop="jobParams">
              <span slot="label">
                <el-tooltip content="eg：{ token: abc123}" placement="top">
                  <i class="el-icon-question"></i>
                </el-tooltip>
                传入参数
              </span>
              <el-input v-model="form.jobParams" placeholder="传入参数" />
            </el-form-item>
          </el-col>
          <el-col :lg="24" v-show="form.triggerType == 1">
            <el-form-item label="间隔(Cron)" prop="cron">
              <!-- <el-input placeholder="如10分钟执行一次：0/0 0/10 * * * ?" v-model="form.cron">
                <el-link slot="append" href="https://qqe2.com/cron" type="primary" target="_blank" class="mr10">cron在线生成</el-link>
              </el-input> -->
              <el-input v-model="form.cron" placeholder="请输入cron执行表达式">
                <template slot="append">
                  <el-button type="primary" @click="handleShowCron">
                    生成表达式
                    <i class="el-icon-time el-icon--right"></i>
                  </el-button>
                </template>
              </el-input>
            </el-form-item>
          </el-col>
          <el-col :lg="12">
            <el-form-item label="开始日期" prop="beginTime">
              <span slot="label">
                <el-tooltip content="如果不写开始时间和结束时间，任务将以当前时间开始执行，到9999年结束" placement="top">
                  <i class="el-icon-question"></i>
                </el-tooltip>
                开始日期
              </span>
              <el-date-picker v-model="form.beginTime" style="width:100%" type="date" :picker-options="pickerOptions" placeholder="选择开始日期" />
            </el-form-item>
          </el-col>
          <el-col :lg="12">
            <el-form-item label="结束日期" prop="endTime">
              <el-date-picker v-model="form.endTime" style="width:100%" type="date" placeholder="选择结束日期" />
            </el-form-item>
          </el-col>
          <el-col :lg="24">
            <el-form-item v-show="form.triggerType == 0" label="执行间隔(秒)" prop="intervalSecond">
              <el-input-number v-model="form.intervalSecond" :max="9999999999" step-strictly controls-position="right" :min="1" />
            </el-form-item>
          </el-col>
          <el-col :lg="24">
            <el-form-item label="备注" prop="remark">
              <el-input type="textarea" v-model="form.remark" />
            </el-form-item>
          </el-col>
        </el-row>
      </el-form>
      <div slot="footer" class="dialog-footer">
        <el-button type="text" @click="cancel">取 消</el-button>
        <el-button type="primary" @click="submitForm">确 定</el-button>
      </div>
    </el-dialog>

    <el-dialog title="Cron表达式生成器" :visible.sync="openCron" append-to-body destroy-on-close class="scrollbar">
      <crontab @hide="openCron=false" @fill="crontabFill" :expression="expression"></crontab>
    </el-dialog>

    <el-drawer :title="logTitle" :visible.sync="drawer">
      <el-timeline>
        <el-timeline-item :timestamp="item.createTime" placement="top" v-for="(item, i) in jobLogList" :key="i">
          <h4>{{item.jobMessage}}</h4>
          <p>{{item.exception}}</p>
        </el-timeline-item>
      </el-timeline>
      <el-empty v-if="jobLogList.length <= 0"></el-empty>
    </el-drawer>
  </div>
</template>

<script>
import {
  queryTasks,
  getTasks,
  createTasks,
  updateTasks,
  deleteTasks,
  startTasks,
  stopTasks,
  runTasks,
  exportTasks
} from '@/api/monitor/job'
import { listJobLog } from '@/api/monitor/jobLog'
import Crontab from '@/components/Crontab'

export default {
  name: 'job',
  components: { Crontab },
  data() {
    var cronValidate = (rule, value, callback) => {
      if (this.form.triggerType === 1) {
        if (value === '' || value === undefined) {
          callback(new Error('运行时间表达式不能为空!'))
        } else {
          callback()
        }
      } else {
        callback()
      }
    }
    var beginTimeValidate = (rule, value, callback) => {
      if (this.form.triggerType === 0) {
        if (value === '' || value === undefined) {
          callback(new Error('选择开始日期!'))
        } else {
          callback()
        }
      } else {
        callback()
      }
    }
    var endTimeValidate = (rule, value, callback) => {
      if (this.form.triggerType === 0) {
        if (value === '' || value === undefined) {
          callback(new Error('选择结束日期!'))
        } else {
          callback()
        }
      } else {
        callback()
      }
    }
    var intervalSecondValidate = (rule, value, callback) => {
      if (this.form.triggerType === 0) {
        if (value === '' || value === undefined) {
          callback(new Error('请设置执行间隔!'))
        } else {
          callback()
        }
      } else {
        callback()
      }
    }
    return {
      // 是否显示Cron表达式弹出层
      openCron: false,
      // 传入的表达式
      expression: '',
      drawer: false,
      // 选中数组
      ids: [],
      // 非单个禁用
      single: true,
      // 非多个禁用
      multiple: true,
      // 是否显示弹出层
      open: false,
      // 表单
      form: {},
      // 表单标题
      title: '',
      // 显示搜索
      searchToggle: true,
      // 表格高度
      tableHeight: window.innerHeight,
      // 合计条数
      total: 0,
      // 遮罩层
      loading: true,
      // 查询参数
      queryParams: {
        queryText: undefined,
        PageNum: 1,
        pageSize: 10,
        orderby: 'createTime',
        sort: 'descending'
      },
      // 计划任务列表
      dataTasks: [],
      // 任务日志列表
      jobLogList: [],
      logTitle: '',
      // 任务状态字典
      isStartOptions: [
        { dictLabel: '运行中', dictValue: 'true' },
        { dictLabel: '已停止', dictValue: 'false', listClass: 'danger' }
      ],
      // 任务组名字典
      jobGroupOptions: [],
      // 触发器类型
      triggerTypeOptions: [
        {
          label: '[普通]',
          value: 0
        },
        {
          label: '[表达式]',
          value: 1
        }
      ],
      // 表单校验
      rules: {
        name: [
          { required: true, message: '任务名称不能为空', trigger: 'blur' }
        ],
        jobGroup: [
          { required: true, message: '任务分组不能为空', trigger: 'blur' }
        ],
        assemblyName: [
          { required: true, message: '程序集名称不能为空', trigger: 'blur' }
        ],
        className: [
          { required: true, message: '任务类名不能为空', trigger: 'blur' }
        ],
        triggerType: [
          { required: true, message: '请选择触发器类型', trigger: 'blur' }
        ],
        apiUrl: [
          { required: true, message: '请输入apiUrl地址', trigger: 'blur' }
        ],
        cron: [{ validator: cronValidate, trigger: 'blur' }],
        beginTime: [{ validator: beginTimeValidate, trigger: 'blur' }],
        endTime: [{ validator: endTimeValidate, trigger: 'blur' }],
        intervalSecond: [
          {
            validator: intervalSecondValidate,
            type: 'number',
            trigger: 'blur'
          }
        ]
      },
      // 时间的选择
      pickerOptions: {
        disabledDate(time) {
          return time.getTime() < Date.now() - 8.64e7
        }
      }
    }
  },
  created() {
    this.getList()
    this.getDicts('sys_job_group').then((response) => {
      this.jobGroupOptions = response.data
    })
  },
  watch: {
    'form.triggerType': {
      handler(val) {
        console.log(val)
        if (val == 0) {
          this.form.cron = undefined
        }
      },
      deep: true,
      immediate: true
    }
  },
  methods: {
    /** 查询计划任务列表 */
    getList() {
      this.loading = true
      queryTasks(this.queryParams).then((response) => {
        this.dataTasks = response.data.result
        this.total = response.data.totalNum
        this.loading = false
      })
    },
    handleQuery() {
      this.getList()
    },
    /** 重置按钮操作 */
    handleReset() {
      this.queryParams.queryText = ''
      this.getList()
    },
    /** 新增按钮操作 */
    handleCreate() {
      this.reset()
      this.open = true
      this.title = '添加计划任务'
    },
    /** 修改按钮操作 */
    handleUpdate(row) {
      this.reset()

      getTasks(row.id).then((res) => {
        this.form = res.data
        this.open = true
        this.title = '修改计划任务'
      })
    },
    /** 任务日志列表查询 */
    handleJobLog(id, title) {
      if (id == undefined) {
        this.$router.push({ path: 'job/log' })
      } else {
        this.drawer = true
        this.jobLogList = []
        this.logTitle = title
        listJobLog({ JobId: id }).then((response) => {
          this.jobLogList = response.data.result
        })
      }
    },
    /** cron表达式按钮操作 */
    handleShowCron() {
      this.expression = this.form.cron
      this.openCron = true
    },
    /** 确定后回传值 */
    crontabFill(value) {
      console.log(value)
      this.form.cron = value
    },
    // 启动按钮
    handleStart(row) {
      startTasks(row.id).then((response) => {
        if (response.code === 200) {
          this.msgSuccess(response.msg)
          this.open = false
          this.getList()
        }
      })
    },
    // 停止按钮
    handleStop(row) {
      stopTasks(row.id).then((response) => {
        if (response.code === 200) {
          this.msgSuccess(response.msg)
          this.open = false
          this.getList()
        }
      })
    },
    /** 删除按钮操作 */
    handleDelete(row) {
      const jobInfo = row

      this.$confirm(
        '是否确认删除名称为"' + jobInfo.name + '"的计划任务?',
        '警告',
        {
          confirmButtonText: '确定',
          cancelButtonText: '取消',
          type: 'warning'
        }
      )
        .then(() => {
          deleteTasks(jobInfo.id).then((response) => {
            if (response.code === 200) {
              this.getList()
              this.msgSuccess('删除成功')
            }
          })
        })
        .catch(function() {})
    },
    /* 立即执行一次 */
    handleRun(row) {
      const jobInfo = row

      this.$confirm('确认要立即执行一次"' + jobInfo.name + '"任务吗?', '警告', {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning'
      }).then((res) => {
        runTasks(jobInfo.id).then((res) => {
          if (res.code === 200) {
            this.msgSuccess('执行成功')
            this.getList()
          }
        })
      })
    },
    /** 提交按钮 */
    submitForm: function() {
      this.$refs['form'].validate((valid) => {
        if (valid) {
          if (this.form.id !== undefined) {
            updateTasks(this.form).then((response) => {
              if (response.code === 200) {
                this.msgSuccess('修改成功')
                this.open = false
                this.getList()
              }
            })
          } else {
            createTasks(this.form).then((response) => {
              if (response.code === 200) {
                this.msgSuccess('新增成功')
                this.open = false
                this.getList()
              }
            })
          }
        }
      })
    },
    // 排序操作
    handleSortable(val) {
      this.queryParams.orderby = val.prop
      this.queryParams.sort = val.order
      this.getList()
    },
    // 表单重置
    reset() {
      this.form = {
        id: undefined,
        name: undefined,
        jobGroup: undefined,
        assemblyName: 'ZR.Tasks',
        className: undefined,
        jobParams: undefined,
        triggerType: 1,
        beginTime: undefined,
        endTime: undefined,
        intervalSecond: 1,
        cron: undefined,
        taskType: 1
      }
      this.resetForm('form')
    },
    // 自动计算分页 Id
    handleIndexCalc(index) {
      return (
        (this.queryParams.PageNum - 1) * this.queryParams.pageSize + index + 1
      )
    },
    // 取消按钮
    cancel() {
      this.open = false
      this.reset()
    },
    /** 导出按钮操作 */
    handleExport() {
      this.$confirm('是否确认导出所有任务?', '警告', {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning'
      })
        .then(() => {
          return exportTasks()
        })
        .then((response) => {
          this.download(response.data.path)
        })
    }
  }
}
</script>
