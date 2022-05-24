--CREATE DATABASE ZrAdmin
GO
USE ZrAdmin
GO

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'定时任务表' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Sys_tasks'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'任务ID', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Sys_tasks', @level2type=N'COLUMN',@level2name=N'ID'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'任务名', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Sys_tasks', @level2type=N'COLUMN',@level2name=N'Name'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'任务分组', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Sys_tasks', @level2type=N'COLUMN',@level2name=N'JobGroup'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'运行时间表达式', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Sys_tasks', @level2type=N'COLUMN',@level2name=N'Cron'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'程序集名称', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Sys_tasks', @level2type=N'COLUMN',@level2name=N'AssemblyName'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'任务所在类', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Sys_tasks', @level2type=N'COLUMN',@level2name=N'ClassName'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'任务描述', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Sys_tasks', @level2type=N'COLUMN',@level2name=N'Remark'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'执行次数', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Sys_tasks', @level2type=N'COLUMN',@level2name=N'RunTimes'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'开始时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Sys_tasks', @level2type=N'COLUMN',@level2name=N'BeginTime'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'结束时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Sys_tasks', @level2type=N'COLUMN',@level2name=N'EndTime'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'触发器类型（0、simple 1、cron）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Sys_tasks', @level2type=N'COLUMN',@level2name=N'TriggerType'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'执行间隔时间(单位:秒)', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Sys_tasks', @level2type=N'COLUMN',@level2name=N'IntervalSecond'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'是否启动', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Sys_tasks', @level2type=N'COLUMN',@level2name=N'IsStart'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'传入参数', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Sys_tasks', @level2type=N'COLUMN',@level2name=N'JobParams'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'创建时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Sys_tasks', @level2type=N'COLUMN',@level2name=N'create_time'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'最后更新时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Sys_tasks', @level2type=N'COLUMN',@level2name=N'update_time'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'创建人编码', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Sys_tasks', @level2type=N'COLUMN',@level2name=N'create_by'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'更新人编码', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Sys_tasks', @level2type=N'COLUMN',@level2name=N'update_by'


-- 定时任务
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'定时任务调度日志表' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_Tasks_log'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'任务日志ID', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_Tasks_log', @level2type=N'COLUMN',@level2name=N'jobLogId'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'任务id', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_Tasks_log', @level2type=N'COLUMN',@level2name=N'jobId'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'任务名称', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_Tasks_log', @level2type=N'COLUMN',@level2name=N'jobName'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'任务组名', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_Tasks_log', @level2type=N'COLUMN',@level2name=N'jobGroup'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'日志信息', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_Tasks_log', @level2type=N'COLUMN',@level2name=N'jobMessage'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'执行状态（0正常 1失败）', @level0type=N'SCHEMA', 
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_Tasks_log', @level2type=N'COLUMN',@level2name=N'status'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'异常信息', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_Tasks_log', @level2type=N'COLUMN',@level2name=N'exception'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'创建时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_Tasks_log', @level2type=N'COLUMN',@level2name=N'createTime'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'调用目标', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_Tasks_log', @level2type=N'COLUMN',@level2name=N'invokeTarget'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'作业用时', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_Tasks_log', @level2type=N'COLUMN',@level2name=N'elapsed'

GO

/*部门表*/
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'部门表' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dept'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'部门id', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dept', @level2type=N'COLUMN',@level2name=N'deptId'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'父部门id', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dept', @level2type=N'COLUMN',@level2name=N'parentId'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'祖级列表', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dept', @level2type=N'COLUMN',@level2name=N'ancestors'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'部门名称', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dept', @level2type=N'COLUMN',@level2name=N'deptName'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'显示顺序', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dept', @level2type=N'COLUMN',@level2name=N'orderNum'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'负责人', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dept', @level2type=N'COLUMN',@level2name=N'leader'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'联系电话', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dept', @level2type=N'COLUMN',@level2name=N'phone'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'部门状态（0正常 1停用）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dept', @level2type=N'COLUMN',@level2name=N'email'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'备注', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dept', @level2type=N'COLUMN',@level2name=N'status'

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'备注', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dept', @level2type=N'COLUMN',@level2name=N'remark'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'删除标志（0代表存在 2代表删除）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dept', @level2type=N'COLUMN',@level2name=N'delFlag'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'创建时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dept', @level2type=N'COLUMN',@level2name=N'create_time'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'最后更新时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dept', @level2type=N'COLUMN',@level2name=N'update_time'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'创建人编码', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dept', @level2type=N'COLUMN',@level2name=N'create_by'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'更新人编码', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dept', @level2type=N'COLUMN',@level2name=N'update_by'

GO

--字典类型表
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'字典类型表' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dict_type'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'字典主键', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dict_type', @level2type=N'COLUMN',@level2name=N'dictId'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'字典名称', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dict_type', @level2type=N'COLUMN',@level2name=N'dictName'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'字典类型', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dict_type', @level2type=N'COLUMN',@level2name=N'dictType'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'状态（0正常 1停用）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dict_type', @level2type=N'COLUMN',@level2name=N'status'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'备注', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dict_type', @level2type=N'COLUMN',@level2name=N'remark'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'创建时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dict_type', @level2type=N'COLUMN',@level2name=N'create_time'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'最后更新时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dict_type', @level2type=N'COLUMN',@level2name=N'update_time'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'创建人编码', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dict_type', @level2type=N'COLUMN',@level2name=N'create_by'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'更新人编码', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dict_type', @level2type=N'COLUMN',@level2name=N'update_by'
GO

/**字典数据表*/
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'字典数据表' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dict_data'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'字典编码', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dict_data', @level2type=N'COLUMN',@level2name=N'dictCode'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'字典排序', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dict_data', @level2type=N'COLUMN',@level2name=N'dictSort'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'字典标签', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dict_data', @level2type=N'COLUMN',@level2name=N'dictLabel'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'字典键值', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dict_data', @level2type=N'COLUMN',@level2name=N'dictValue'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'字典类型', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dict_data', @level2type=N'COLUMN',@level2name=N'dictType'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'样式属性（其他样式扩展）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dict_data', @level2type=N'COLUMN',@level2name=N'cssClass'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'表格回显样式', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dict_data', @level2type=N'COLUMN',@level2name=N'listClass'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'是否默认（Y是 N否）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dict_data', @level2type=N'COLUMN',@level2name=N'isDefault'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'状态（0正常 1停用）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dict_data', @level2type=N'COLUMN',@level2name=N'status'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'备注', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dict_data', @level2type=N'COLUMN',@level2name=N'remark'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'创建时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dict_data', @level2type=N'COLUMN',@level2name=N'create_time'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'最后更新时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dict_data', @level2type=N'COLUMN',@level2name=N'update_time'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'创建人编码', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dict_data', @level2type=N'COLUMN',@level2name=N'create_by'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'更新人编码', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_dict_data', @level2type=N'COLUMN',@level2name=N'update_by'
GO

--登录日志表
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'用户登录记录表' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_logininfor'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'访问ID', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_logininfor', @level2type=N'COLUMN',@level2name=N'infoId'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'用户账号', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_logininfor', @level2type=N'COLUMN',@level2name=N'userName'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'登录IP地址', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_logininfor', @level2type=N'COLUMN',@level2name=N'ipaddr'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'登录地点', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_logininfor', @level2type=N'COLUMN',@level2name=N'loginLocation'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'浏览器类型', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_logininfor', @level2type=N'COLUMN',@level2name=N'browser'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'操作系统', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_logininfor', @level2type=N'COLUMN',@level2name=N'os'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'登录状态（0成功 1失败）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_logininfor', @level2type=N'COLUMN',@level2name=N'status'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'提示消息', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_logininfor', @level2type=N'COLUMN',@level2name=N'msg'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'访问时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_logininfor', @level2type=N'COLUMN',@level2name=N'loginTime'
GO
-- 菜单表
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'菜单表' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_menu'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'菜单ID', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_menu', @level2type=N'COLUMN',@level2name=N'menuId'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'菜单名称', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_menu', @level2type=N'COLUMN',@level2name=N'menuName'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'父菜单ID', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_menu', @level2type=N'COLUMN',@level2name=N'parentId'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'显示顺序', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_menu', @level2type=N'COLUMN',@level2name=N'orderNum'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'路由地址', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_menu', @level2type=N'COLUMN',@level2name=N'path'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'组件路径', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_menu', @level2type=N'COLUMN',@level2name=N'component'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'是否外链(0 否 1 是)', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_menu', @level2type=N'COLUMN',@level2name=N'isFrame'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'是否缓存(0缓存 1不缓存)', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_menu', @level2type=N'COLUMN',@level2name=N'isCache'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'菜单类型（M目录 C菜单 F按钮 L链接）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_menu', @level2type=N'COLUMN',@level2name=N'menuType'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'菜单状态（0显示 1隐藏）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_menu', @level2type=N'COLUMN',@level2name=N'visible'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'菜单状态（0正常 1停用）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_menu', @level2type=N'COLUMN',@level2name=N'status'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'权限标识', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_menu', @level2type=N'COLUMN',@level2name=N'perms'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'菜单图标', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_menu', @level2type=N'COLUMN',@level2name=N'icon'

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'创建时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_menu', @level2type=N'COLUMN',@level2name=N'create_time'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'最后更新时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_menu', @level2type=N'COLUMN',@level2name=N'update_time'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'创建人编码', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_menu', @level2type=N'COLUMN',@level2name=N'create_by'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'更新人编码', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_menu', @level2type=N'COLUMN',@level2name=N'update_by'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'备注', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_menu', @level2type=N'COLUMN',@level2name=N'remark'
GO

-- 操作日志记录
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'操作日志记录' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_oper_log'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'日志主键', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_oper_log', @level2type=N'COLUMN',@level2name=N'operId'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'模块标题', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_oper_log', @level2type=N'COLUMN',@level2name=N'title'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'业务类型（0其它 1新增 2修改 3删除）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_oper_log', @level2type=N'COLUMN',@level2name=N'businessType'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'方法名称', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_oper_log', @level2type=N'COLUMN',@level2name=N'method'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'请求方式', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_oper_log', @level2type=N'COLUMN',@level2name=N'requestMethod'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'操作类别（0其它 1后台用户 2手机端用户）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_oper_log', @level2type=N'COLUMN',@level2name=N'operatorType'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'操作人员', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_oper_log', @level2type=N'COLUMN',@level2name=N'operName'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'部门名称', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_oper_log', @level2type=N'COLUMN',@level2name=N'deptName'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'请求URL', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_oper_log', @level2type=N'COLUMN',@level2name=N'operUrl'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'主机地址', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_oper_log', @level2type=N'COLUMN',@level2name=N'operIP'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'操作地点', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_oper_log', @level2type=N'COLUMN',@level2name=N'operLocation'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'请求参数', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_oper_log', @level2type=N'COLUMN',@level2name=N'operParam'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'返回参数', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_oper_log', @level2type=N'COLUMN',@level2name=N'jsonResult'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'操作状态（0正常 1异常）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_oper_log', @level2type=N'COLUMN',@level2name=N'status'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'错误消息', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_oper_log', @level2type=N'COLUMN',@level2name=N'errorMsg'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'操作时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_oper_log', @level2type=N'COLUMN',@level2name=N'operTime'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'请求用时', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_oper_log', @level2type=N'COLUMN',@level2name=N'elapsed'
GO

-- 岗位信息表
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'岗位信息表' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_post'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'岗位ID', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_post', @level2type=N'COLUMN',@level2name=N'postId'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'岗位编码', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_post', @level2type=N'COLUMN',@level2name=N'postCode'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'岗位名称', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_post', @level2type=N'COLUMN',@level2name=N'postName'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'显示顺序', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_post', @level2type=N'COLUMN',@level2name=N'postSort'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'状态（0正常 1停用）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_post', @level2type=N'COLUMN',@level2name=N'status'

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'创建时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_post', @level2type=N'COLUMN',@level2name=N'create_time'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'最后更新时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_post', @level2type=N'COLUMN',@level2name=N'update_time'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'创建人编码', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_post', @level2type=N'COLUMN',@level2name=N'create_by'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'更新人编码', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_post', @level2type=N'COLUMN',@level2name=N'update_by'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'备注', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_post', @level2type=N'COLUMN',@level2name=N'remark'
GO

/**用户表*/
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'用户表' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_user'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'用户ID', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_user', @level2type=N'COLUMN',@level2name=N'userId'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'部门ID', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_user', @level2type=N'COLUMN',@level2name=N'deptId'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'用户账号', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_user', @level2type=N'COLUMN',@level2name=N'userName'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'用户昵称', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_user', @level2type=N'COLUMN',@level2name=N'nickName'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'用户类型（00系统用户）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_user', @level2type=N'COLUMN',@level2name=N'userType'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'用户邮箱', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_user', @level2type=N'COLUMN',@level2name=N'email'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'手机号码', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_user', @level2type=N'COLUMN',@level2name=N'phonenumber'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'用户性别（0男 1女 2未知）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_user', @level2type=N'COLUMN',@level2name=N'sex'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'头像地址', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_user', @level2type=N'COLUMN',@level2name=N'avatar'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'密码', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_user', @level2type=N'COLUMN',@level2name=N'password'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'帐号状态（0正常 1停用）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_user', @level2type=N'COLUMN',@level2name=N'status'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'删除标志（0代表存在 2代表删除）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_user', @level2type=N'COLUMN',@level2name=N'delFlag'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'最后登录IP', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_user', @level2type=N'COLUMN',@level2name=N'loginIP'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'最后登录时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_user', @level2type=N'COLUMN',@level2name=N'loginDate'

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'创建时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_user', @level2type=N'COLUMN',@level2name=N'create_time'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'最后更新时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_user', @level2type=N'COLUMN',@level2name=N'update_time'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'创建人编码', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_user', @level2type=N'COLUMN',@level2name=N'create_by'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'更新人编码', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_user', @level2type=N'COLUMN',@level2name=N'update_by'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'备注', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_user', @level2type=N'COLUMN',@level2name=N'remark'
GO


EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'用户岗位绑定表' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_user_post'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'用户ID', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_user_post', @level2type=N'COLUMN',@level2name=N'userId'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'岗位ID', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_user_post', @level2type=N'COLUMN',@level2name=N'postId'
GO


-- ----------------------------
-- '角色信息表'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'角色信息表' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_role'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'角色ID', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_role', @level2type=N'COLUMN',@level2name=N'roleId'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'角色名称', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_role', @level2type=N'COLUMN',@level2name=N'roleName'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'角色权限字符串', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_role', @level2type=N'COLUMN',@level2name=N'roleKey'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'显示顺序', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_role', @level2type=N'COLUMN',@level2name=N'roleSort'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'数据范围（1：全部数据权限 2：自定数据权限 3：本部门数据权限 ）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_role', @level2type=N'COLUMN',@level2name=N'dataScope'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'菜单树选择项是否关联显示', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_role', @level2type=N'COLUMN',@level2name=N'menu_check_strictly'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'部门树选择项是否关联显示', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_role', @level2type=N'COLUMN',@level2name=N'dept_check_strictly'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'角色状态（0正常 1停用）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_role', @level2type=N'COLUMN',@level2name=N'status'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'删除标志（0代表存在 2代表删除）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_role', @level2type=N'COLUMN',@level2name=N'delFlag'

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'创建时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_role', @level2type=N'COLUMN',@level2name=N'create_time'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'最后更新时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_role', @level2type=N'COLUMN',@level2name=N'update_time'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'创建人编码', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_role', @level2type=N'COLUMN',@level2name=N'create_by'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'更新人编码', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_role', @level2type=N'COLUMN',@level2name=N'update_by'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'备注', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_role', @level2type=N'COLUMN',@level2name=N'remark'
GO


EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'用户和角色关联表' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_user_role'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'用户ID', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_user_role', @level2type=N'COLUMN',@level2name=N'user_id'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'角色ID', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_user_role', @level2type=N'COLUMN',@level2name=N'role_id'

go

-- 角色和菜单关联表
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'角色和菜单关联表' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_role_menu'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'角色ID', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_role_menu', @level2type=N'COLUMN',@level2name=N'role_id'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'菜单ID', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_role_menu', @level2type=N'COLUMN',@level2name=N'menu_id'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'创建人编码', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_role_menu', @level2type=N'COLUMN',@level2name=N'create_by'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'创建时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_role_menu', @level2type=N'COLUMN',@level2name=N'create_time'
GO

-- 通知公告表
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'公告ID' , @level0type=N'SCHEMA',@level0name=N'dbo', 
@level1type=N'TABLE',@level1name=N'sys_notice', @level2type=N'COLUMN',@level2name=N'notice_id'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'公告标题' , @level0type=N'SCHEMA',@level0name=N'dbo', 
@level1type=N'TABLE',@level1name=N'sys_notice', @level2type=N'COLUMN',@level2name=N'notice_title'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'公告类型 (1通知 2公告)' , @level0type=N'SCHEMA',@level0name=N'dbo', 
@level1type=N'TABLE',@level1name=N'sys_notice', @level2type=N'COLUMN',@level2name=N'notice_type'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'公告内容' , @level0type=N'SCHEMA',@level0name=N'dbo', 
@level1type=N'TABLE',@level1name=N'sys_notice', @level2type=N'COLUMN',@level2name=N'notice_content'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'公告状态' , @level0type=N'SCHEMA',@level0name=N'dbo', 
@level1type=N'TABLE',@level1name=N'sys_notice', @level2type=N'COLUMN',@level2name=N'status'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'创建者' , @level0type=N'SCHEMA',@level0name=N'dbo', 
@level1type=N'TABLE',@level1name=N'sys_notice', @level2type=N'COLUMN',@level2name=N'create_by'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'创建时间' , @level0type=N'SCHEMA',@level0name=N'dbo', 
@level1type=N'TABLE',@level1name=N'sys_notice', @level2type=N'COLUMN',@level2name=N'create_time'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'更新者' , @level0type=N'SCHEMA',@level0name=N'dbo', 
@level1type=N'TABLE',@level1name=N'sys_notice', @level2type=N'COLUMN',@level2name=N'update_by'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'更新时间' , @level0type=N'SCHEMA',@level0name=N'dbo', 
@level1type=N'TABLE',@level1name=N'sys_notice', @level2type=N'COLUMN',@level2name=N'update_time'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'备注' , @level0type=N'SCHEMA',@level0name=N'dbo', 
@level1type=N'TABLE',@level1name=N'sys_notice', @level2type=N'COLUMN',@level2name=N'remark'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'通知公告表' , @level0type=N'SCHEMA',@level0name=N'dbo', 
@level1type=N'TABLE',@level1name=N'sys_notice'
GO

-- 文章表
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'文章表' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'article'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'文章id', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'article', @level2type=N'COLUMN',@level2name=N'cid'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'文章标题', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'article', @level2type=N'COLUMN',@level2name=N'title'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'用户id', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'article', @level2type=N'COLUMN',@level2name=N'userId'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'文章状态1、已发布 2、草稿', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'article', @level2type=N'COLUMN',@level2name=N'status'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'编辑器类型markdown,html', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'article', @level2type=N'COLUMN',@level2name=N'fmt_type'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'文章标签', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'article', @level2type=N'COLUMN',@level2name=N'tags'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'点击量', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'article', @level2type=N'COLUMN',@level2name=N'hits'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'目录id', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'article', @level2type=N'COLUMN',@level2name=N'category_id'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'创建时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'article', @level2type=N'COLUMN',@level2name=N'createTime'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'修改时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'article', @level2type=N'COLUMN',@level2name=N'updateTime'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'创建时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'article', @level2type=N'COLUMN',@level2name=N'authorName'
GO

--
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'目录表' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'articleCategory'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'目录id', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'articleCategory', @level2type=N'COLUMN',@level2name=N'category_id'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'目录名', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'articleCategory', @level2type=N'COLUMN',@level2name=N'name'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'创建时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'articleCategory', @level2type=N'COLUMN',@level2name=N'create_time'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'父级ID', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'articleCategory', @level2type=N'COLUMN',@level2name=N'parentId'
GO


-- 18、代码生成业务表
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'代码生成业务表' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'编号', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table', @level2type=N'COLUMN',@level2name=N'tableId'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'表名称', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table', @level2type=N'COLUMN',@level2name=N'tableName'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'表描述', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table', @level2type=N'COLUMN',@level2name=N'tableComment'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'关联子表的表名', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table', @level2type=N'COLUMN',@level2name=N'subTableName'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'子表关联的外键名', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table', @level2type=N'COLUMN',@level2name=N'subTableFkName'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'实体类名称', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table', @level2type=N'COLUMN',@level2name=N'className'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'使用的模板（crud单表操作 tree树表操作）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table', @level2type=N'COLUMN',@level2name=N'tplCategory'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'生成命名空间前缀', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table', @level2type=N'COLUMN',@level2name=N'baseNameSpace'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'生成模块名', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table', @level2type=N'COLUMN',@level2name=N'moduleName'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'生成业务名', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table', @level2type=N'COLUMN',@level2name=N'businessName'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'生成功能名', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table', @level2type=N'COLUMN',@level2name=N'functionName'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'生成功能作者', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table', @level2type=N'COLUMN',@level2name=N'functionAuthor'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'生成代码方式（0zip压缩包 1自定义路径）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table', @level2type=N'COLUMN',@level2name=N'genType'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'生成路径（不填默认项目路径）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table', @level2type=N'COLUMN',@level2name=N'genPath'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'其它生成选项', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table', @level2type=N'COLUMN',@level2name=N'options'

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'创建时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table', @level2type=N'COLUMN',@level2name=N'create_time'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'最后更新时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table', @level2type=N'COLUMN',@level2name=N'update_time'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'创建人编码', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table', @level2type=N'COLUMN',@level2name=N'create_by'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'更新人编码', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table', @level2type=N'COLUMN',@level2name=N'update_by'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'备注', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table', @level2type=N'COLUMN',@level2name=N'remark'

GO


-- 代码生成业务表字段
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'代码生成业务表字段' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table_column'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'编号', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table_column', @level2type=N'COLUMN',@level2name=N'columnId'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'归属表编号', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table_column', @level2type=N'COLUMN',@level2name=N'tableId'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'表名', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table_column', @level2type=N'COLUMN',@level2name=N'tableName'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'列名称', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table_column', @level2type=N'COLUMN',@level2name=N'columnName'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'列描述', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table_column', @level2type=N'COLUMN',@level2name=N'columnComment'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'列类型', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table_column', @level2type=N'COLUMN',@level2name=N'columnType'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'C#类型', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table_column', @level2type=N'COLUMN',@level2name=N'csharpType'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'C#字段名', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table_column', @level2type=N'COLUMN',@level2name=N'csharpField'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'是否主键（1是）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table_column', @level2type=N'COLUMN',@level2name=N'isPk'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'是否自增（1是）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table_column', @level2type=N'COLUMN',@level2name=N'isIncrement'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'是否必填（1是）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table_column', @level2type=N'COLUMN',@level2name=N'isRequired'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'是否为插入字段（1是）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table_column', @level2type=N'COLUMN',@level2name=N'isInsert'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'是否编辑字段（1是）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table_column', @level2type=N'COLUMN',@level2name=N'isEdit'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'是否列表字段（1是）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table_column', @level2type=N'COLUMN',@level2name=N'isList'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'是否查询字段（1是）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table_column', @level2type=N'COLUMN',@level2name=N'isQuery'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'查询方式（等于、不等于、大于、小于、范围）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table_column', @level2type=N'COLUMN',@level2name=N'queryType'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'显示类型（文本框、文本域、下拉框、复选框、单选框、日期控件）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table_column', @level2type=N'COLUMN',@level2name=N'htmlType'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'字典类型', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table_column', @level2type=N'COLUMN',@level2name=N'dictType'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'排序', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table_column', @level2type=N'COLUMN',@level2name=N'sort'

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'创建时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table_column', @level2type=N'COLUMN',@level2name=N'create_time'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'最后更新时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table_column', @level2type=N'COLUMN',@level2name=N'update_time'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'创建人编码', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table_column', @level2type=N'COLUMN',@level2name=N'create_by'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'更新人编码', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table_column', @level2type=N'COLUMN',@level2name=N'update_by'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'备注', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'gen_table_column', @level2type=N'COLUMN',@level2name=N'remark'
GO

-- 参数配置表
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'参数配置表' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_config'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'参数主键', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_config', @level2type=N'COLUMN',@level2name=N'configId'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'参数名称', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_config', @level2type=N'COLUMN',@level2name=N'configName'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'参数键名', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_config', @level2type=N'COLUMN',@level2name=N'configKey'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'参数键值', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_config', @level2type=N'COLUMN',@level2name=N'configValue'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'系统内置（Y是 N否）', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_config', @level2type=N'COLUMN',@level2name=N'configType'

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'创建时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_config', @level2type=N'COLUMN',@level2name=N'create_time'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'最后更新时间', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_config', @level2type=N'COLUMN',@level2name=N'update_time'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'创建人编码', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_config', @level2type=N'COLUMN',@level2name=N'create_by'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'更新人编码', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_config', @level2type=N'COLUMN',@level2name=N'update_by'
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'备注', @level0type=N'SCHEMA',
@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'sys_config', @level2type=N'COLUMN',@level2name=N'remark'

GO

-- 代码生成测试

EXEC sp_addextendedproperty
'MS_Description', N'自增id',
'SCHEMA', N'dbo',
'TABLE', N'gen_demo',
'COLUMN', N'id'
GO

EXEC sp_addextendedproperty
'MS_Description', N'名称',
'SCHEMA', N'dbo',
'TABLE', N'gen_demo',
'COLUMN', N'name'
GO

EXEC sp_addextendedproperty
'MS_Description', N'图片',
'SCHEMA', N'dbo',
'TABLE', N'gen_demo',
'COLUMN', N'icon'
GO

EXEC sp_addextendedproperty
'MS_Description', N'显示状态',
'SCHEMA', N'dbo',
'TABLE', N'gen_demo',
'COLUMN', N'showStatus'
GO

EXEC sp_addextendedproperty
'MS_Description', N'添加时间',
'SCHEMA', N'dbo',
'TABLE', N'gen_demo',
'COLUMN', N'addTime'
GO

EXEC sp_addextendedproperty
'MS_Description', N'用户性别',
'SCHEMA', N'dbo',
'TABLE', N'gen_demo',
'COLUMN', N'sex'
GO

EXEC sp_addextendedproperty
'MS_Description', N'排序',
'SCHEMA', N'dbo',
'TABLE', N'gen_demo',
'COLUMN', N'sort'
GO

EXEC sp_addextendedproperty
'MS_Description', N'开始时间',
'SCHEMA', N'dbo',
'TABLE', N'gen_demo',
'COLUMN', N'beginTime'
GO

EXEC sp_addextendedproperty
'MS_Description', N'结束时间',
'SCHEMA', N'dbo',
'TABLE', N'gen_demo',
'COLUMN', N'endTime'
GO

EXEC sp_addextendedproperty
'MS_Description', N'备注',
'SCHEMA', N'dbo',
'TABLE', N'gen_demo',
'COLUMN', N'remark'
GO
