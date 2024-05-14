--CREATE DATABASE ZrAdmin
--GO
USE ZrAdmin
GO
if OBJECT_ID(N'sys_tasks',N'U') is not NULL DROP TABLE sys_tasks
GO
CREATE TABLE sys_tasks
(
	id VARCHAR(100) NOT NULL PRIMARY KEY,	--任务ID
	name VARCHAR(50) NOT NULL,				--任务名
	jobGroup varchar(255) NOT NULL,			--'任务分组',
	cron varchar(255) NOT NULL ,			--'运行时间表达式',
	assemblyName varchar(255)  NOT NULL ,	--'程序集名称',
	className varchar(255)  NOT NULL ,		--'任务所在类',
	remark VARCHAR(200)  NULL ,				--'任务描述',
	runTimes int NOT NULL ,					--'执行次数',
	beginTime datetime NULL DEFAULT NULL ,	--'开始时间',
	endTime datetime NULL DEFAULT NULL ,	--'结束时间',
	triggerType int NOT NULL ,				--'触发器类型（0、simple 1、cron）',
	intervalSecond int NOT NULL ,			--'执行间隔时间(单位:秒)',
	isStart int NOT NULL ,					--'是否启动',
	jobParams TEXT  NULL ,					--'传入参数',
	create_time datetime NULL DEFAULT NULL , --'创建时间',
	update_time datetime NULL DEFAULT NULL , --'最后更新时间',
	create_by varchar(50)  NULL DEFAULT NULL , --'创建人编码',
	update_by varchar(50)  NULL DEFAULT NULL , --'更新人编码',
	lastRunTime datetime					,  --最后执行时间
	taskType int null						,  --任务类型 1程序集 2网络请求
	apiUrl	varchar(200),					--网络请求地址
	sqlText VARCHAR(1000),					--sql语句
	requestMethod VARCHAR(10)				--请求方法
)
GO
if OBJECT_ID(N'sys_tasks_log',N'U') is not NULL DROP TABLE sys_tasks_log
GO
/**定时任务调度日志表*/
CREATE TABLE sys_tasks_log  (
  jobLogId bigint NOT NULL PRIMARY KEY IDENTITY(1,1), -- '任务日志ID',
  jobId varchar(20)  NOT NULL  ,		-- '任务id',
  jobName varchar(64) NOT NULL ,		-- '任务名称',
  jobGroup varchar(64) NOT NULL ,		-- '任务组名',
  jobMessage varchar(500)  NULL  ,		-- '日志信息',
  status varchar(1) NULL DEFAULT '0' ,		-- '执行状态（0正常 1失败）',
  exception varchar(2000) NULL DEFAULT '' ,  -- '异常信息',
  createTime datetime NULL ,			-- '创建时间',
  invokeTarget varchar(200)  NULL ,		-- '调用目标',
  elapsed DECIMAL(10, 4) NULL,			-- '作业用时',
)
GO

/*公告表*/
if OBJECT_ID(N'sys_notice',N'U') is not NULL DROP TABLE sys_notice
GO
CREATE TABLE [dbo].[sys_notice](
	[notice_id] [int] NOT NULL PRIMARY KEY IDENTITY(1,1),
	[notice_title] [varchar](100) NULL,
	[notice_type] int NULL,
	[notice_content] [text] NULL,
	[status] int NULL,
	[create_by] [varchar](64) NULL,
	[create_time] [datetime] NULL,
	[update_by] [varchar](64) NULL,
	[update_time] [datetime] NULL,
	[remark] [varchar](255) NULL,
)
GO

/*部门表*/
if OBJECT_ID(N'sys_dept',N'U') is not NULL DROP TABLE sys_dept
GO
CREATE TABLE sys_dept  (
  deptId bigint NOT NULL PRIMARY KEY IDENTITY(100,1) ,		-- '部门id',
  parentId bigint NULL DEFAULT 0 ,				-- '父部门id',
  ancestors varchar(50)  NULL DEFAULT '' ,			-- '祖级列表',
  deptName varchar(30)  NULL DEFAULT '' ,			-- '部门名称',
  orderNum int NULL DEFAULT 0 ,					-- '显示顺序',
  leader varchar(20)  NULL DEFAULT NULL ,			-- '负责人',
  phone varchar(11)  NULL DEFAULT NULL ,			-- '联系电话',
  email varchar(50)  NULL DEFAULT NULL ,			-- '邮箱',
  status int  NULL DEFAULT 0 ,					-- '部门状态（0正常 1停用）',
  delFlag int  NULL DEFAULT 0 ,					-- '删除标志（0代表存在 2代表删除）',
  create_by varchar(64)  NULL DEFAULT '' ,			-- '创建者',
  create_time datetime NULL DEFAULT NULL ,			-- '创建时间',
  update_by varchar(64)  NULL DEFAULT '' ,			-- '更新者',
  update_time datetime NULL DEFAULT NULL ,			-- '更新时间',
  remark varchar(255)  NULL DEFAULT NULL ,			-- '备注',
) 
GO

IF OBJECT_ID(N'sys_dict_type',N'U') is not NULL DROP TABLE sys_dict_type
GO
--字典类型表
CREATE TABLE sys_dict_type  (
  dictId BIGINT NOT NULL PRIMARY KEY IDENTITY(1,1) ,-- '字典主键',
  dictName varchar(100)  NULL DEFAULT '' ,-- '字典名称',
  dictType varchar(100)  NULL DEFAULT '' ,-- '字典类型',
  status varchar(1)  NULL DEFAULT '0'    ,-- '状态（0正常 1停用）',
  type   varchar(1)  NULL default 'N'    ,-- '系统内置（Y是 N否）',
  create_by varchar(64)  NULL DEFAULT '' ,-- '创建者',
  create_time datetime NULL DEFAULT NULL ,-- '创建时间',
  update_by varchar(64)  NULL DEFAULT '' ,-- '更新者',
  update_time datetime NULL DEFAULT NULL ,-- '更新时间',
  remark varchar(500)  NULL DEFAULT NULL ,-- '备注',
  customSql varchar(500)  NULL DEFAULT NULL ,-- '自定义sql',
)
GO
--CREATE UNIQUE INDEX dictType ON dbo.sys_dict_type(dictType)
--GO

if OBJECT_ID(N'sys_dict_data',N'U') is not NULL DROP TABLE sys_dict_data
GO
/**字典数据表*/
CREATE TABLE sys_dict_data  (
  dictCode bigint NOT NULL IDENTITY(1,1) PRIMARY KEY , --'字典编码',
  dictSort int NULL DEFAULT 0 ,							-- '字典排序',
  dictLabel varchar(100)  NULL DEFAULT '' ,				-- '字典标签',
  dictValue varchar(100)  NULL DEFAULT '' ,				-- '字典键值',
  dictType varchar(100)  NULL DEFAULT '' ,				-- '字典类型',
  cssClass varchar(100)  NULL DEFAULT NULL ,			-- '样式属性（其他样式扩展）',
  listClass varchar(100)  NULL DEFAULT NULL ,-- '表格回显样式',
  isDefault varchar(1)  NULL DEFAULT 'N' ,-- '是否默认（Y是 N否）',
  status varchar(1)  NULL DEFAULT '0' ,-- '状态（0正常 1停用）',
  create_by varchar(64)  NULL DEFAULT '' ,-- '创建者',
  create_time datetime NULL DEFAULT NULL ,-- '创建时间',
  update_by varchar(64)  NULL DEFAULT '' ,-- '更新者',
  update_time datetime NULL DEFAULT NULL ,-- '更新时间',
  remark varchar(500)  NULL DEFAULT NULL ,-- '备注',
  langKey VARCHAR(50) NULL					--翻译key
)
GO

if OBJECT_ID(N'sys_logininfor',N'U') is not NULL DROP TABLE sys_logininfor
GO
CREATE TABLE sys_logininfor  (
  infoId bigint NOT NULL PRIMARY KEY IDENTITY(1,1) ,-- '访问ID',
  userName varchar(50)  NULL DEFAULT '' ,-- '用户账号',
  ipaddr varchar(50)  NULL DEFAULT '' ,-- '登录IP地址',
  loginLocation varchar(255)  NULL DEFAULT '' ,-- '登录地点',
  browser varchar(500)  NULL DEFAULT '' ,-- '浏览器类型',
  os varchar(500)  NULL DEFAULT '' ,-- '操作系统',
  status varchar(1)  NULL DEFAULT '0' ,-- '登录状态（0成功 1失败）',
  msg varchar(255)  NULL DEFAULT '' ,-- '提示消息',
  loginTime DATETIME NULL DEFAULT NULL  ,-- '访问时间',
) 
GO
if OBJECT_ID(N'sys_menu',N'U') is not NULL DROP TABLE sys_menu
GO
CREATE TABLE sys_menu  (
  menuId bigint NOT NULL PRIMARY KEY IDENTITY(1,1) ,-- '菜单ID',
  menuName varchar(50)  NOT NULL ,-- '菜单名称',
  parentId bigint NULL DEFAULT 0 ,-- '父菜单ID',
  orderNum int NULL DEFAULT 0 ,-- '显示顺序',
  path varchar(200)  NULL DEFAULT '' ,-- '路由地址',
  component varchar(255)  NULL DEFAULT NULL ,-- '组件路径',
  isFrame int NULL DEFAULT 0 ,-- '是否外链(0 否 1 是)',
  isCache int NULL DEFAULT 0 ,-- '是否缓存(0缓存 1不缓存)',
  menuType varchar(1)  NULL DEFAULT '' ,-- '菜单类型（M目录 C菜单 F按钮 L链接）',
  visible varchar(1)  NULL DEFAULT '0' ,-- '菜单状态（0显示 1隐藏）',
  status varchar(1)  NULL DEFAULT '0' ,-- '菜单状态（0正常 1停用）',
  perms varchar(100)  NULL DEFAULT NULL ,-- '权限标识',
  icon varchar(100)  NULL DEFAULT '#' ,-- '菜单图标',
  create_by varchar(64)  NULL DEFAULT '' ,-- '创建者',
  create_time datetime NULL DEFAULT NULL ,-- '创建时间',
  update_by varchar(64)  NULL DEFAULT '' ,-- '更新者',
  update_time datetime NULL DEFAULT NULL ,-- '更新时间',
  remark varchar(500)  NULL DEFAULT '' ,-- '备注',
  menuName_key VARCHAR(50) NULL DEFAULT('') --翻译key
) 
GO

-- ---------------------------- 
-- = '操作日志记录' 
-- Table structure for sys_oper_log
-- ----------------------------
if OBJECT_ID(N'sys_oper_log',N'U') is not NULL DROP TABLE sys_oper_log
GO
CREATE TABLE sys_oper_log  (
  operId bigint NOT NULL PRIMARY KEY IDENTITY(1,1),--'日志主键',
  title varchar(50)  DEFAULT '' , -- '模块标题',
  businessType int NULL DEFAULT 0 , -- '业务类型（0其它 1新增 2修改 3删除）',
  method varchar(100)  DEFAULT '' , -- '方法名称',
  requestMethod varchar(10)  DEFAULT '' , -- '请求方式',
  operatorType int NULL DEFAULT 0 , -- '操作类别（0其它 1后台用户 2手机端用户）',
  operName varchar(50)  DEFAULT '' , -- '操作人员',
  deptName varchar(50)  DEFAULT '' , -- '部门名称',
  operUrl varchar(255)  DEFAULT '' , -- '请求URL',
  operIP varchar(50)  DEFAULT '' , -- '主机地址',
  operLocation varchar(255)  DEFAULT '' , -- '操作地点',
  operParam varchar(2000)  DEFAULT '' , -- '请求参数',
  jsonResult TEXT  DEFAULT '' , -- '返回参数',
  status int NULL DEFAULT 0 , -- '操作状态（0正常 1异常）',
  errorMsg varchar(2000)  DEFAULT '' , -- '错误消息',
  operTime datetime NULL DEFAULT NULL , -- '操作时间',
  elapsed bigint NULL DEFAULT NULL , -- '请求用时',
) 
GO

-- ----------------------------
-- = '岗位信息表'
-- Table structure for sys_post
-- ----------------------------
if OBJECT_ID(N'sys_post',N'U') is not NULL DROP TABLE sys_post
GO
CREATE TABLE sys_post  (
  postId bigint NOT NULL IDENTITY(1,1) PRIMARY KEY, -- '岗位ID',
  postCode varchar(64)  NOT NULL , -- '岗位编码',
  postName varchar(50)  NOT NULL , -- '岗位名称',
  postSort int NOT NULL , -- '显示顺序',
  status varchar(1)  NOT NULL , -- '状态（0正常 1停用）',
  create_by varchar(64)  DEFAULT '' , -- '创建者',
  create_time datetime NULL DEFAULT NULL , -- '创建时间',
  update_by varchar(64)  DEFAULT '' , -- '更新者',
  update_time datetime NULL DEFAULT NULL , -- '更新时间',
  remark varchar(500)  DEFAULT NULL , -- '备注',
)
GO
CREATE UNIQUE INDEX postCode ON dbo.sys_post(postCode)
GO

/**用户表*/
IF OBJECT_ID(N'sys_user',N'U') is not NULL DROP TABLE sys_user
GO
CREATE TABLE sys_user  (
  userId BIGINT NOT NULL IDENTITY(1,1),	-- '用户ID',
  deptId BIGINT NULL DEFAULT 0 ,		--'部门ID',
  userName varchar(30) NOT NULL,				-- '用户账号',
  nickName varchar(30) NOT NULL,				-- '用户昵称',
  userType varchar(2) DEFAULT '0' ,		--'用户类型（00系统用户）',
  email varchar(50) ,					-- '用户邮箱',
  phonenumber varchar(11) ,				-- '手机号码',
  sex int DEFAULT 0,			-- '用户性别（0男 1女 2未知）',
  avatar varchar(400) ,					-- '头像地址',
  password varchar(100) NOT NULL,				-- '密码',
  status int DEFAULT 0,		-- '帐号状态（0正常 1停用）',
  delFlag INT NULL DEFAULT 0,  -- ,  -- '删除标志（0代表存在 2代表删除）',
  loginIP varchar(50) ,					-- '最后登录IP',
  loginDate datetime NULL ,				--'最后登录时间',
  create_by varchar(64) ,				-- '创建者',
  create_time datetime NULL ,			-- '创建时间',
  update_by varchar(64) ,				-- '更新者',
  update_time datetime NULL ,			-- '更新时间',
  remark varchar(500) ,					-- '备注',
  province VARCHAR(50) ,
  city VARCHAR(50)
)
GO
CREATE UNIQUE INDEX index_userName ON dbo.sys_user(userName)
GO
IF OBJECT_ID(N'sys_user_post',N'U') is not NULL DROP TABLE sys_user_post
GO
CREATE TABLE sys_user_post  (
  userId bigint NOT NULL ,  -- '用户ID',
  postId bigint NOT NULL ,  -- '岗位ID',
)
GO
ALTER TABLE sys_user_post add primary key(userId,postId)
GO

-- ----------------------------
-- '角色信息表'
-- Table structure for sys_role
-- ----------------------------
IF OBJECT_ID(N'sys_role',N'U') is not NULL DROP TABLE sys_role
GO
CREATE TABLE sys_role  (
  roleId bigint NOT NULL PRIMARY KEY IDENTITY(1,1) , -- '角色ID',
  roleName varchar(30)  NOT NULL , -- '角色名称',
  roleKey varchar(100)  NOT NULL , -- '角色权限字符串',
  roleSort int NOT NULL , -- '显示顺序',
  dataScope int  NULL DEFAULT 1 , -- '数据范围（1：全部数据权限 2：自定数据权限 3：本部门数据权限 ）',
  menu_check_strictly BIT NOT NULL DEFAULT 1 , -- '菜单树选择项是否关联显示',
  dept_check_strictly BIT NOT NULL DEFAULT 1 , -- '部门树选择项是否关联显示',
  status int  NOT NULL , -- '角色状态（0正常 1停用）',
  delFlag int  NOT NULL DEFAULT 0 , -- '删除标志（0代表存在 2代表删除）',
  create_by varchar(64)  NULL DEFAULT '' , -- '创建者',
  create_time datetime NULL DEFAULT NULL , -- '创建时间',
  update_by varchar(64)  NULL DEFAULT '' , -- '更新者',
  update_time datetime NULL DEFAULT NULL , -- '更新时间',
  remark varchar(500)  NULL DEFAULT NULL , -- '备注',
) 
GO
-- ----------------------------
-- Table structure for sys_role_dept
-- ----------------------------
IF OBJECT_ID(N'sys_role_dept',N'U') is not NULL DROP TABLE sys_role_dept
GO
CREATE TABLE sys_role_dept  (
  roleId bigint NOT NULL, 				-- '角色ID',
  deptId bigint NOT NULL ,				-- '部门ID',
)

GO
-- ----------------------------
-- 用户和角色关联表
-- Table structure for sys_user_role
-- ----------------------------
IF OBJECT_ID(N'sys_user_role',N'U') is not NULL DROP TABLE sys_user_role
GO
CREATE TABLE sys_user_role  (
  user_id bigint NOT NULL ,  -- '用户ID',
  role_id bigint NOT NULL ,  -- '角色ID',
  create_by varchar(64)  NULL DEFAULT '' , -- '创建者',
  create_time datetime NULL DEFAULT NULL , -- '创建时间',
  update_by varchar(64)  NULL DEFAULT '' , -- '更新者',
  update_time datetime NULL DEFAULT NULL , -- '更新时间',
  remark varchar(500)  NULL DEFAULT NULL , -- '备注',
)
go
alter table sys_user_role add primary key(user_id,role_id)
GO
-- ----------------------------
-- 角色和菜单关联表
-- Table structure for sys_role_menu
-- ----------------------------
IF OBJECT_ID(N'sys_role_menu',N'U') is not NULL DROP TABLE sys_role_menu
GO
CREATE TABLE sys_role_menu  (
  role_id bigint NOT NULL , -- '角色ID',
  menu_id bigint NOT NULL , -- '菜单ID',
  create_by varchar(20) DEFAULT NULL,
  create_time datetime NULL DEFAULT NULL,
  update_by VARCHAR(20) DEFAULT NULL,
  update_time DATETIME NULL ,
  remark VARCHAR(100)
)
GO
alter table sys_role_menu add primary key(menu_id,role_id)
GO

-- ----------------------------
-- 文章表
-- Table structure for article
-- ----------------------------
IF OBJECT_ID(N'article',N'U') is not NULL DROP TABLE article
GO
CREATE TABLE article  (
  cid int NOT NULL IDENTITY(1,1) PRIMARY KEY,
  title varchar(254) DEFAULT NULL ,  -- '文章标题',
  content text ,  -- '文章内容',
  userId bigint NULL DEFAULT NULL ,  -- '用户id',
  status varchar(20) DEFAULT NULL ,  -- '文章状态1、已发布 2、草稿',
  fmt_type varchar(20) DEFAULT NULL ,  -- '编辑器类型markdown,html',
  tags varchar(100) DEFAULT NULL ,  -- '文章标签',
  hits int NULL DEFAULT NULL ,  -- '点击量',
  category_id int NULL DEFAULT NULL ,  -- '目录id',
  createTime datetime NULL DEFAULT NULL ,  -- '创建时间',
  updateTime datetime NULL DEFAULT NULL ,  -- '修改时间',
  authorName varchar(20) DEFAULT NULL ,  -- '作者名',
  coverUrl varchar(300) NULL, 			--文章封面
  isPublic int default(0),				--是否公开
  abstractText NVARCHAR(100) NULL,		--摘要
)
GO
-- ----------------------------
-- Table structure for articleCategory
-- ----------------------------
IF OBJECT_ID(N'articleCategory',N'U') is not NULL DROP TABLE dbo.articleCategory
GO
CREATE TABLE dbo.articleCategory  (
  category_id int NOT NULL IDENTITY(1,1) PRIMARY KEY ,  -- '目录id',
  name varchar(20) NOT NULL ,  -- '目录名',
  create_time datetime NULL DEFAULT NULL ,  -- '创建时间',
  parentId int NULL DEFAULT 0 ,  -- '父级ID',
  icon nvarchar(200) NULL,
  orderNum int NULL,
  bgImg nvarchar(max) NULL,
  introduce nvarchar(200) NULL,
  categoryType int NULL,
  articleNum int NULL,
  joinNum int NULL,
)
GO

-- ----------------------------
-- 18、代码生成业务表
-- ----------------------------
IF OBJECT_ID(N'gen_table',N'U') is not NULL DROP TABLE dbo.gen_table
GO
create table dbo.gen_table (
  tableId          bigint      not NULL PRIMARY KEY IDENTITY(1,1)    , --'编号',
  tableName        varchar(200)    default ''                 , --'表名称',
  tableComment     varchar(500)    default ''                 , --'表描述',
  subTableName    varchar(64)     default null               , --'关联子表的表名',
  subTableFkName varchar(64)     default null               , --'子表关联的外键名',
  className        varchar(100)    default ''                 , --'实体类名称',
  tplCategory      varchar(200)    default 'crud'             , --'使用的模板（crud单表操作 tree树表操作）',
  baseNameSpace      varchar(100)                             , --'生成命名空间前缀',
  moduleName       varchar(30)                                , --'生成模块名',
  businessName     varchar(30)                                , --'生成业务名',
  functionName     varchar(50)                                , --'生成功能名',
  functionAuthor   varchar(50)                                , --'生成功能作者',
  genType          varchar(1)         default '0'             , --'生成代码方式（0zip压缩包 1自定义路径）',
  genPath          varchar(200)    default '/'                , --'生成路径（不填默认项目路径）',
  options           varchar(1000)                              , --'其它生成选项',
  create_by         varchar(64)     default ''                 , --'创建者',
  create_time 	    datetime                                   , --'创建时间',
  update_by         VARCHAR(64)     DEFAULT ''                 , --'更新者',
  update_Time       datetime                                   , --'更新时间',
  remark            varchar(500)    default null               , --'备注',
  dbName			VARCHAR(100)							   , --数据库名
)
GO
-- ----------------------------
-- 代码生成业务表字段
-- ----------------------------
IF OBJECT_ID(N'gen_table_column',N'U') is not NULL DROP TABLE dbo.gen_table_column
GO
create table dbo.gen_table_column (
  columnId         bigint      not null IDENTITY(1,1) PRIMARY KEY    , --'编号',
  tableId          BIGINT									, --'归属表编号',
  tableName		   VARCHAR(200)								, --表名
  columnName       varchar(200)                               , --'列名称',
  columnComment    varchar(500)                               , --'列描述',
  columnType       varchar(100)                               , --'列类型',
  csharpType	   VARCHAR(500)                               , --'C#类型',
  csharpField	   VARCHAR(200)                               , --'C#字段名',
  isPk             TINYINT                                    , --'是否主键（1是）',
  isIncrement      TINYINT                                    , --'是否自增（1是）',
  isRequired       TINYINT                                    , --'是否必填（1是）',
  isInsert         TINYINT                                    , --'是否为插入字段（1是）',
  isEdit           TINYINT                                    , --'是否编辑字段（1是）',
  isList           TINYINT                                    , --'是否列表字段（1是）',
  isSort           TINYINT                                    , --'是否排序字段（1是）',
  isQuery          TINYINT                                    ,-- '是否查询字段（1是）',
  isExport         TINYINT                                    ,-- '是否导出字段（1是）',
  queryType        varchar(200)    default 'EQ'               , --'查询方式（等于、不等于、大于、小于、范围）',
  htmlType         varchar(200)                               , --'显示类型（文本框、文本域、下拉框、复选框、单选框、日期控件）',
  dictType         varchar(200)    default ''                 , --'字典类型',
  sort              int                                        , --'排序',
  create_by         varchar(64)     default ''                 , --'创建者',
  create_time 	    datetime                                   , --'创建时间',
  update_by         varchar(64)     default ''                 , --'更新者',
  update_time       datetime                                   , --'更新时间',
  remark			VARCHAR(200)								,--
  autoFillType		INT											--自动填充类型 1、添加 2、编辑 3、添加编辑
)
GO
-- ----------------------------
-- 参数配置表
-- ----------------------------
IF OBJECT_ID(N'sys_config',N'U') is not NULL DROP TABLE dbo.sys_config
GO
create table dbo.sys_config (
  configId         int          not null IDENTITY(1,1) PRIMARY KEY    ,-- '参数主键',
  configName       varchar(100)    default ''                 ,-- '参数名称',
  configKey        varchar(100)    default ''                 ,-- '参数键名',
  configValue      varchar(500)    default ''                 ,-- '参数键值',
  configType       varchar(1)      default 'N'                ,-- '系统内置（Y是 N否）',
  create_by         varchar(64)    default ''                 ,-- '创建者',
  create_time       datetime                                   ,--mment '创建时间',
  update_by         varchar(64)    default ''                 ,-- '更新者',
  update_time       datetime                                   ,-- '更新时间',
  remark            varchar(500)   default null               ,-- '备注',
)
GO

-- ----------------------------
-- 代码生成测试
-- Table structure for gen_demo
-- ----------------------------
IF EXISTS (SELECT * FROM sys.all_objects WHERE object_id = OBJECT_ID(N'[dbo].[gen_demo]') AND type IN ('U'))
	DROP TABLE [dbo].[gen_demo]
GO

CREATE TABLE [dbo].[gen_demo] (
  [id] int  IDENTITY(1,1) NOT NULL PRIMARY KEY,
  [name] varchar(20)  NULL,
  [icon] varchar(255) NULL,
  [showStatus] int  NOT NULL,
  [addTime] datetime  NULL,
  [sex] int DEFAULT NULL NULL,
  [sort] int DEFAULT ((0)) NULL,
  [beginTime] datetime  NULL,
  [endTime] datetime  NULL,
  [remark] varchar(200)  NULL,
  [feature] varchar(100) NULL
)
GO

ALTER TABLE [dbo].[gen_demo] SET (LOCK_ESCALATION = TABLE)
GO
-- ----------------------------
-- 文件存储
-- Table structure for [sys_file]
-- ----------------------------
IF EXISTS (SELECT * FROM sys.all_objects WHERE object_id = OBJECT_ID(N'[dbo].[sys_file]') AND type IN ('U'))
	DROP TABLE [dbo].[sys_file]
GO

CREATE TABLE [dbo].[sys_file](
	[id] [BIGINT] NOT NULL PRIMARY KEY,
	[realName] VARCHAR(50) NULL,
	[fileName] [VARCHAR](50) NULL,
	[fileUrl] [VARCHAR](500) NULL,
	[storePath] [VARCHAR](50) NULL,
	[accessUrl] [VARCHAR](300) NULL,
	[fileSize] [VARCHAR](20) NULL,
	[fileType] VARCHAR(200) NULL,
	[fileExt] [VARCHAR](10) NULL,
	[create_by] [VARCHAR](50) NULL,
	[create_time] [DATETIME] NULL,
	[storeType] [INT] NULL
)
GO
IF OBJECT_ID(N'sys_common_lang',N'U') is not NULL DROP TABLE dbo.sys_common_lang
GO
CREATE TABLE dbo.sys_common_lang
(
	id BIGINT NOT NULL,
	lang_code VARCHAR(10) NOT NULL,				--语言code eg：zh-cn
	lang_key NVARCHAR(100) NULL,				--语言翻译key
	lang_name NVARCHAR(2000) NOT NULL,			--
	addtime DATETIME
)
GO

GO
IF OBJECT_ID(N'SqlDiffLog',N'U') is not NULL DROP TABLE dbo.SqlDiffLog
GO
CREATE TABLE [dbo].[SqlDiffLog](
	[PId] [BIGINT] NOT NULL PRIMARY KEY,
	[TableName] [VARCHAR](255) NULL,
	[BusinessData] [VARCHAR](4000) NULL,
	[DiffType] [VARCHAR](255) NULL,
	[Sql] [NVARCHAR](MAX) NULL,
	[BeforeData] [NVARCHAR](MAX) NULL,
	[AfterData] [NVARCHAR](MAX) NULL,
	[UserName] [VARCHAR](255) NULL,
	[AddTime] [DATETIME] NULL,
	[ConfigId] [VARCHAR](255) NULL
)
GO
IF OBJECT_ID(N'email_log',N'U') is not NULL DROP TABLE dbo.email_log
GO
CREATE TABLE [dbo].[email_log](
	[Id] [BIGINT] NOT NULL PRIMARY KEY,
	[FromEmail] [VARCHAR](255) NULL,
	[Subject] [VARCHAR](255) NULL,
	[ToEmails] [NVARCHAR](MAX) NULL,
	[EmailContent] [NVARCHAR](MAX) NULL,
	[AddTime] [DATETIME] NULL,
	[IsSend] [INT] NULL,
	[SendResult] [VARCHAR](255) NULL,
	[FileUrl] [VARCHAR](255) NULL,
	[SendTime] [DATETIME] NULL,
)
GO
IF OBJECT_ID(N'emailTpl',N'U') is not NULL DROP TABLE dbo.emailTpl
GO
CREATE TABLE [dbo].[emailTpl](
	[Id] [INT] IDENTITY(1,1) NOT NULL,
	[Name] [VARCHAR](255) NULL,
	[Content] [NVARCHAR](MAX) NULL,
	[Create_by] [VARCHAR](64) NULL,
	[Create_time] [DATETIME] NULL,
	[Update_by] [VARCHAR](64) NULL,
	[Update_time] [DATETIME] NULL,
	[Remark] [VARCHAR](500) NULL,
 )
GO
IF OBJECT_ID(N'smsCode_log',N'U') is not NULL DROP TABLE dbo.smsCode_log
GO
CREATE TABLE [dbo].[smsCode_log](
	[Id] [BIGINT] NOT NULL,
	[SmsCode] [VARCHAR](255) NULL,
	[Userid] [BIGINT] NULL,
	[PhoneNum] [BIGINT] NULL,
	[SmsContent] [VARCHAR](255) NULL,
	[AddTime] [DATETIME] NULL,
	[UserIP] [VARCHAR](255) NULL,
	[Location] [VARCHAR](255) NULL,
	[SendType] [INT] NULL
)
IF OBJECT_ID(N'article_topic',N'U') is not NULL DROP TABLE dbo.article_topic
GO
CREATE TABLE [dbo].[article_topic](
	[topicId] [int] IDENTITY(1,1) PRIMARY KEY NOT NULL,
	[topicName] [varchar](20) NOT NULL,
	[topicDescription] [varchar](500) NULL,
	[joinNum] [int] NULL,
	[viewNum] [int] NULL, 
	[addTime] [datetime] NULL,
	[topicType] [int] NULL,  
) 
GO
 
 