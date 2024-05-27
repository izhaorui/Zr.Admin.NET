
SET NAMES utf8mb4;

-- ----------------------------
-- Table structure for sys_tasks
-- ----------------------------
DROP TABLE IF EXISTS `sys_tasks`;
CREATE TABLE `sys_tasks`  (
  `id` varchar(20) CHARACTER SET utf8 COLLATE utf8_general_ci NOT NULL DEFAULT '' COMMENT 'UID',
  `name` varchar(255) CHARACTER SET utf8 COLLATE utf8_general_ci NOT NULL COMMENT '任务名称',
  `jobGroup` varchar(255) CHARACTER SET utf8 COLLATE utf8_general_ci NOT NULL COMMENT '任务分组',
  `cron` varchar(255) CHARACTER SET utf8 COLLATE utf8_general_ci NULL DEFAULT NULL COMMENT '运行时间表达式',
  `assemblyName` varchar(255) CHARACTER SET utf8 COLLATE utf8_general_ci NOT NULL COMMENT '程序集名称',
  `className` varchar(255) CHARACTER SET utf8 COLLATE utf8_general_ci NOT NULL COMMENT '任务所在类',
  `remark` longtext CHARACTER SET utf8 COLLATE utf8_general_ci NULL COMMENT '任务描述',
  `runTimes` int(11) NOT NULL COMMENT '执行次数',
  `beginTime` datetime(0) NULL DEFAULT NULL COMMENT '开始时间',
  `endTime` datetime(0) NULL DEFAULT NULL COMMENT '结束时间',
  `triggerType` int(11) NOT NULL COMMENT '触发器类型（0、simple 1、cron）',
  `IntervalSecond` int(11) NOT NULL COMMENT '执行间隔时间(单位:秒)',
  `IsStart` tinyint(4) NOT NULL COMMENT '是否启动',
  `JobParams` longtext CHARACTER SET utf8 COLLATE utf8_general_ci NULL COMMENT '传入参数',
  `create_time` datetime(0) NULL DEFAULT NULL COMMENT '创建时间',
  `update_time` datetime(0) NULL DEFAULT NULL COMMENT '最后更新时间',
  `create_by` varchar(50) CHARACTER SET utf8 COLLATE utf8_general_ci NULL DEFAULT NULL COMMENT '创建人编码',
  `update_by` varchar(50) CHARACTER SET utf8 COLLATE utf8_general_ci NULL DEFAULT NULL COMMENT '更新人编码',
  `lastRunTime` datetime(0) NULL DEFAULT NULL COMMENT '最后执行时间',
  `apiUrl` varchar(255) CHARACTER SET utf8 COLLATE utf8_general_ci NULL DEFAULT NULL COMMENT 'api执行地址',
  `taskType` int(4) NULL DEFAULT 1 COMMENT '任务类型1程序集任务 2网络请求',
  `sqlText` varchar(1000) CHARACTER SET utf8 COLLATE utf8_general_ci NULL COMMENT 'SQL语句',
  `requestMethod` varchar(20) CHARACTER SET utf8 COLLATE utf8_general_ci NULL COMMENT 'http请求方法',
  PRIMARY KEY (`ID`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8 COLLATE = utf8_general_ci COMMENT = '计划任务' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for sys_tasks_log
-- ----------------------------
DROP TABLE
IF
	EXISTS `sys_tasks_log`;
CREATE TABLE `sys_tasks_log` (
	`jobLogId` BIGINT ( 20 ) NOT NULL AUTO_INCREMENT COMMENT '任务日志ID',
	`jobId` VARCHAR ( 20 ) CHARACTER 
	SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '任务id',
	`jobName` VARCHAR ( 64 ) CHARACTER 
	SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '任务名称',
	`jobGroup` VARCHAR ( 64 ) CHARACTER 
	SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '任务组名',
	`jobMessage` VARCHAR ( 500 ) CHARACTER 
	SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '日志信息',
	`status` CHAR ( 1 ) CHARACTER 
	SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '0' COMMENT '执行状态（0正常 1失败）',
	`exception` VARCHAR ( 2000 ) CHARACTER 
	SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '异常信息',
	`createTime` DATETIME ( 0 ) NULL DEFAULT NULL COMMENT '创建时间',
	`invokeTarget` VARCHAR ( 200 ) CHARACTER 
	SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '调用目标',
	`elapsed` DOUBLE ( 10, 0 ) NULL DEFAULT NULL COMMENT '作业用时',
	PRIMARY KEY ( `jobLogId` ) USING BTREE 
) ENGINE = INNODB AUTO_INCREMENT = 198 CHARACTER 
SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '定时任务调度日志表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- 通知公告表
-- ----------------------------
DROP TABLE
IF
	EXISTS sys_notice;
CREATE TABLE sys_notice (
	notice_id INT ( 4 ) NOT NULL auto_increment COMMENT '公告ID',
	notice_title VARCHAR ( 50 ) CHARACTER 
	SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '公告标题',
	notice_type INT NOT NULL COMMENT '公告类型（1通知 2公告）',
	notice_content TEXT CHARACTER 
	SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '公告内容',
	`status` INT DEFAULT 0 COMMENT '公告状态（0正常 1关闭）',
	create_by VARCHAR ( 64 ) DEFAULT '' COMMENT '创建者',
	create_time DATETIME COMMENT '创建时间',
	update_by VARCHAR ( 64 ) DEFAULT '' COMMENT '更新者',
	update_time DATETIME COMMENT '更新时间',
	remark VARCHAR ( 255 ) CHARACTER 
	SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
	PRIMARY KEY ( notice_id ) 
) ENGINE = INNODB auto_increment = 1 COMMENT = '通知公告表';


-- ----------------------------
-- Table structure for sys_dept
-- ----------------------------
DROP TABLE IF EXISTS `sys_dept`;
CREATE TABLE `sys_dept`  (
  `deptId` bigint(20) NOT NULL AUTO_INCREMENT COMMENT '部门id',
  `parentId` bigint(20) NULL DEFAULT 0 COMMENT '父部门id',
  `ancestors` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '祖级列表',
  `deptName` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '部门名称',
  `orderNum` int(4) NULL DEFAULT 0 COMMENT '显示顺序',
  `leader` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '负责人',
  `phone` varchar(11) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '联系电话',
  `email` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '邮箱',
  `status` int(4) NULL DEFAULT 0 COMMENT '部门状态（0正常 1停用）',
  `delFlag` int(4) NULL DEFAULT 0 COMMENT '删除标志（0代表存在 2代表删除）',
  `create_by` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '创建者',
  `create_time` datetime(0) NULL DEFAULT NULL COMMENT '创建时间',
  `update_by` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '更新者',
  `update_time` datetime(0) NULL DEFAULT NULL COMMENT '更新时间',
  `remark` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '备注',
  PRIMARY KEY (`deptId`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 204 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '部门表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for sys_dict_data
-- ----------------------------
DROP TABLE IF EXISTS `sys_dict_data`;
CREATE TABLE `sys_dict_data`  (
  `dictCode` bigint(20) NOT NULL AUTO_INCREMENT COMMENT '字典编码',
  `dictSort` int(4) NULL DEFAULT 0 COMMENT '字典排序',
  `dictLabel` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '字典标签',
  `dictValue` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '字典键值',
  `dictType` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '字典类型',
  `cssClass` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '样式属性（其他样式扩展）',
  `listClass` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '表格回显样式',
  `isDefault` char(1) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT 'N' COMMENT '是否默认（Y是 N否）',
  `status` char(1) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '0' COMMENT '状态（0正常 1停用）',
  `create_by` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '创建者',
  `create_time` datetime(0) NULL DEFAULT NULL COMMENT '创建时间',
  `update_by` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '更新者',
  `update_time` datetime(0) NULL DEFAULT NULL COMMENT '更新时间',
  `remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '备注',
  `langKey` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '翻译key',
  PRIMARY KEY (`dictCode`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 31 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '字典数据表' ROW_FORMAT = Dynamic;


SET FOREIGN_KEY_CHECKS = 1;

-- ----------------------------
-- Table structure for sys_dict_type
-- ----------------------------
DROP TABLE IF EXISTS `sys_dict_type`;
CREATE TABLE `sys_dict_type`  (
  `dictId` bigint(20) NOT NULL AUTO_INCREMENT COMMENT '字典主键',
  `dictName` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '字典名称',
  `dictType` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '字典类型',
  `status` char(1) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '0' COMMENT '状态（0正常 1停用）',
  `type`   char(1) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT 'N' COMMENT '系统内置（Y是 N否）',
  `create_by` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '创建者',
  `create_time` datetime(0) NULL DEFAULT NULL COMMENT '创建时间',
  `update_by` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '更新者',
  `update_time` datetime(0) NULL DEFAULT NULL COMMENT '更新时间',
  `remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '备注',
  `customSql` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '自定义sql语句',
  PRIMARY KEY (`dictId`) USING BTREE,
  UNIQUE INDEX `dictType`(`dictType`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 12 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '字典类型表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for sys_logininfor
-- ----------------------------
DROP TABLE IF EXISTS `sys_logininfor`;
CREATE TABLE `sys_logininfor`  (
  `infoId` bigint(20) NOT NULL AUTO_INCREMENT COMMENT '访问ID',
  `userName` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '用户账号',
  `ipaddr` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '登录IP地址',
  `loginLocation` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '登录地点',
  `browser` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '浏览器类型',
  `os` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '操作系统',
  `status` char(1) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '0' COMMENT '登录状态（0成功 1失败）',
  `msg` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '提示消息',
  `loginTime` datetime(0) NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP(0) COMMENT '访问时间',
  PRIMARY KEY (`infoId`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 17 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '系统访问记录' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for sys_menu
-- ----------------------------
DROP TABLE IF EXISTS `sys_menu`;
CREATE TABLE `sys_menu`  (
  `menuId` bigint(20) NOT NULL AUTO_INCREMENT COMMENT '菜单ID',
  `menuName` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '菜单名称',
  `parentId` bigint(20) NULL DEFAULT 0 COMMENT '父菜单ID',
  `orderNum` int(4) NULL DEFAULT 0 COMMENT '显示顺序',
  `path` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '路由地址',
  `component` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '组件路径',
  `isFrame` int(1) NULL DEFAULT 0 COMMENT '是否外链(0 否 1 是)',
  `isCache` int(1) NULL DEFAULT 0 COMMENT '是否缓存(0缓存 1不缓存)',
  `menuType` varchar(1) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '菜单类型（M目录 C菜单 F按钮 L链接）',
  `visible` varchar(1) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '0' COMMENT '菜单状态（0显示 1隐藏）',
  `status` varchar(1) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '0' COMMENT '菜单状态（0正常 1停用）',
  `perms` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '权限标识',
  `icon` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '#' COMMENT '菜单图标',
  `create_by` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '创建者',
  `create_time` datetime(0) NULL DEFAULT NULL COMMENT '创建时间',
  `update_by` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '更新者',
  `update_time` datetime(0) NULL DEFAULT NULL COMMENT '更新时间',
  `remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '备注',
  `menuName_key` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '菜单名翻译字典名',
  PRIMARY KEY (`menuId`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 2000 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '菜单权限表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for sys_oper_log
-- ----------------------------
DROP TABLE IF EXISTS `sys_oper_log`;
CREATE TABLE `sys_oper_log`  (
  `operId` bigint(20) NOT NULL AUTO_INCREMENT COMMENT '日志主键',
  `title` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '模块标题',
  `businessType` int(2) NULL DEFAULT 0 COMMENT '业务类型（0其它 1新增 2修改 3删除）',
  `method` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '方法名称',
  `requestMethod` varchar(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '请求方式',
  `operatorType` int(1) NULL DEFAULT 0 COMMENT '操作类别（0其它 1后台用户 2手机端用户）',
  `operName` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '操作人员',
  `deptName` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '部门名称',
  `operUrl` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '请求URL',
  `operIP` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '主机地址',
  `operLocation` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '操作地点',
  `operParam` varchar(2000) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '请求参数',
  `jsonResult` varchar(5000) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '返回参数',
  `status` int(1) NULL DEFAULT 0 COMMENT '操作状态（0正常 1异常）',
  `errorMsg` varchar(2000) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '错误消息',
  `operTime` datetime(0) NULL DEFAULT NULL COMMENT '操作时间',
  `elapsed` bigint(20) NULL DEFAULT NULL COMMENT '请求用时',
  PRIMARY KEY (`operId`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '操作日志记录' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for sys_post
-- ----------------------------
DROP TABLE IF EXISTS `sys_post`;
CREATE TABLE `sys_post`  (
  `postId` bigint(20) NOT NULL AUTO_INCREMENT COMMENT '岗位ID',
  `postCode` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '岗位编码',
  `postName` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '岗位名称',
  `postSort` int(4) NOT NULL COMMENT '显示顺序',
  `status` varchar(1) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '状态（0正常 1停用）',
  `create_by` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '创建者',
  `create_time` datetime(0) NULL DEFAULT NULL COMMENT '创建时间',
  `update_by` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '更新者',
  `update_time` datetime(0) NULL DEFAULT NULL COMMENT '更新时间',
  `remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '备注',
  PRIMARY KEY (`postId`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 15 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '岗位信息表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for sys_role
-- ----------------------------
DROP TABLE IF EXISTS `sys_role`;
CREATE TABLE `sys_role`  (
  `roleId` bigint(20) NOT NULL AUTO_INCREMENT COMMENT '角色ID',
  `roleName` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '角色名称',
  `roleKey` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '角色权限字符串',
  `roleSort` int(4) NOT NULL COMMENT '显示顺序',
  `dataScope` int(4)  NULL DEFAULT 1 COMMENT '数据范围（1：全部数据权限 2：自定数据权限 3：本部门数据权限 ）',
  `menu_check_strictly` tinyint(1) NULL DEFAULT 1 COMMENT '菜单树选择项是否关联显示',
  `dept_check_strictly` tinyint(1) NOT NULL DEFAULT 1 COMMENT '部门树选择项是否关联显示',
  `status` int(4)  NOT NULL DEFAULT 0 COMMENT '角色状态（0正常 1停用）',
  `delFlag` int(4) NOT NULL DEFAULT 0 COMMENT '删除标志（0代表存在 2代表删除）',
  `create_by` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '创建者',
  `create_time` datetime(0) NULL DEFAULT NULL COMMENT '创建时间',
  `update_by` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '更新者',
  `update_time` datetime(0) NULL DEFAULT NULL COMMENT '更新时间',
  `remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '备注',
  PRIMARY KEY (`roleId`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 117 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '角色信息表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for sys_role_dept
-- ----------------------------
DROP TABLE IF EXISTS `sys_role_dept`;
CREATE TABLE `sys_role_dept`  (
  `roleId` bigint(20) NOT NULL COMMENT '角色ID',
  `deptId` bigint(20) NOT NULL COMMENT '部门ID',
  PRIMARY KEY (`roleId`, `deptId`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '角色和部门关联表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for sys_role_menu
-- ----------------------------
DROP TABLE IF EXISTS `sys_role_menu`;
CREATE TABLE `sys_role_menu`  (
  `role_id` bigint(20) NOT NULL COMMENT '角色ID',
  `menu_id` bigint(20) NOT NULL COMMENT '菜单ID',
  `create_by` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL,
  `create_time` datetime(0) NULL DEFAULT NULL,
  update_by VARCHAR(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL,
  update_time datetime(0) NULL DEFAULT NULL COMMENT '更新时间',
  remark varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '备注',
  PRIMARY KEY (`role_id`, `menu_id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '角色和菜单关联表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for sys_user
-- ----------------------------
DROP TABLE IF EXISTS `sys_user`;
CREATE TABLE `sys_user`  (
  `userId` bigint(20) NOT NULL AUTO_INCREMENT COMMENT '用户ID',
  `deptId` bigint(20) NULL DEFAULT NULL COMMENT '部门ID',
  `userName` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '用户账号',
  `nickName` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '用户昵称',
  `userType` varchar(2) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '0' COMMENT '用户类型（00系统用户）',
  `email` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '用户邮箱',
  `phonenumber` varchar(11) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '手机号码',
  `sex` int NULL DEFAULT 0 COMMENT '用户性别（0男 1女 2未知）',
  `avatar` varchar(400) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '头像地址',
  `password` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '密码',
  `status` int  NULL DEFAULT 0 COMMENT '帐号状态（0正常 1停用）',
  `delFlag` int NULL DEFAULT 0 COMMENT '删除标志（0代表存在 2代表删除）',
  `loginIP` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '最后登录IP',
  `loginDate` datetime(0) NULL DEFAULT NULL COMMENT '最后登录时间',
  `create_by` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '创建者',
  `create_time` datetime(0) NULL DEFAULT NULL COMMENT '创建时间',
  `update_by` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '更新者',
  `update_time` datetime(0) NULL DEFAULT NULL COMMENT '更新时间',
  `remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '备注',
  `province` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '所在省',
  `city` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '所在市',
  PRIMARY KEY (`userId`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 4 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '用户信息表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for sys_user_post
-- ----------------------------
DROP TABLE IF EXISTS `sys_user_post`;
CREATE TABLE `sys_user_post`  (
  `userId` bigint(20) NOT NULL COMMENT '用户ID',
  `postId` bigint(20) NOT NULL COMMENT '岗位ID',
  PRIMARY KEY (`userId`, `postId`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '用户与岗位关联表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for sys_user_role
-- ----------------------------
DROP TABLE IF EXISTS `sys_user_role`;
CREATE TABLE `sys_user_role`  (
  `user_id` bigint(20) NOT NULL COMMENT '用户ID',
  `role_id` bigint(20) NOT NULL COMMENT '角色ID',
  `create_by` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '创建者',
  `create_time` datetime(0) NULL DEFAULT NULL COMMENT '创建时间',
  `update_by` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '更新者',
  `update_time` datetime(0) NULL DEFAULT NULL COMMENT '更新时间',
  `remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '备注',
  PRIMARY KEY (`user_id`, `role_id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '用户和角色关联表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for article
-- ----------------------------
DROP TABLE IF EXISTS `article`;
CREATE TABLE `article`  (
  `cid` int(11) NOT NULL AUTO_INCREMENT,
  `title` varchar(254) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '文章标题',
  `content` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL COMMENT '文章内容',
  `userId` bigint(11) NULL DEFAULT NULL COMMENT '用户id',
  `status` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '文章状态1、已发布 2、草稿',
  `fmt_type` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '编辑器类型markdown,html',
  `tags` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '文章标签',
  `hits` int(11) NULL DEFAULT NULL COMMENT '点击量',
  `category_id` int(11) NULL DEFAULT NULL COMMENT '目录id',
  `createTime` datetime(6) NULL DEFAULT NULL COMMENT '创建时间',
  `updateTime` datetime(6) NULL DEFAULT NULL COMMENT '修改时间',
  `authorName` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '作者名',
  `coverUrl` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '封面',
  `isPublic` int(4) NULL DEFAULT 0 COMMENT '是否公开',
  `abstractText` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL COMMENT '文章摘要',
  PRIMARY KEY (`cid`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 28 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci ROW_FORMAT = Dynamic;

SET FOREIGN_KEY_CHECKS = 1;

-- ----------------------------
-- Table structure for articleCategory
-- ----------------------------
DROP TABLE IF EXISTS `articleCategory`;
CREATE TABLE `articleCategory`  (
  `category_id` int(4) NOT NULL AUTO_INCREMENT COMMENT '目录id',
  `name` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '目录名',
  `create_time` datetime(6) NULL DEFAULT NULL COMMENT '创建时间',
  `parentId` int(10) UNSIGNED NULL DEFAULT 0 COMMENT '父级ID',
  `icon` varchar(200) NULL,
  `orderNum` int(10) NULL,
  `bgImg` varchar(max) NULL,
  `introduce` varchar(200) NULL,
  `categoryType` int(10) NULL,
  `articleNum` int(10) NULL,
  `joinNum` int(10) NULL,
  PRIMARY KEY (`category_id`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 9 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci ROW_FORMAT = Dynamic;

-- ----------------------------
-- 13、参数配置表
-- ----------------------------
drop table if exists sys_config;
create table sys_config (
  configId         int(5)          not null auto_increment    comment '参数主键',
  configName       varchar(100)    default ''                 comment '参数名称',
  configKey        varchar(100)    default ''                 comment '参数键名',
  configValue      varchar(500)    default ''                 comment '参数键值',
  configType       varchar(1)         default 'N'                comment '系统内置（Y是 N否）',
  create_by         varchar(64)     default ''                 comment '创建者',
  create_time       datetime                                   comment '创建时间',
  update_by         varchar(64)     default ''                 comment '更新者',
  update_time       datetime                                   comment '更新时间',
  remark            varchar(500)    default null               comment '备注',
  primary key (configId)
) engine=innodb auto_increment=100 comment = '参数配置表';


-- ----------------------------
-- 18、代码生成业务表
-- ----------------------------
DROP TABLE
IF
	EXISTS gen_table;
CREATE TABLE gen_table (
	tableId BIGINT ( 20 ) NOT NULL auto_increment COMMENT '编号',
	tableName VARCHAR ( 200 ) DEFAULT '' COMMENT '表名称',
	tableComment VARCHAR ( 500 ) CHARACTER 
	SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '表描述',
	subTableName VARCHAR ( 64 ) CHARACTER 
	SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '关联子表的表名',
	subTableFkName VARCHAR ( 64 ) CHARACTER 
	SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '子表关联的外键名',
	className VARCHAR ( 100 ) DEFAULT '' COMMENT '实体类名称',
	tplCategory VARCHAR ( 200 ) DEFAULT 'crud' COMMENT '使用的模板（crud单表操作 tree树表操作）',
	baseNameSpace VARCHAR ( 100 ) COMMENT '生成命名空间前缀',
	moduleName VARCHAR ( 30 ) CHARACTER 
	SET utf8mb4 COLLATE utf8mb4_general_ci NULL COMMENT '生成模块名',
	businessName VARCHAR ( 30 ) CHARACTER 
	SET utf8mb4 COLLATE utf8mb4_general_ci NULL COMMENT '生成业务名',
	functionName VARCHAR ( 50 ) CHARACTER 
	SET utf8mb4 COLLATE utf8mb4_general_ci NULL COMMENT '生成功能名',
	functionAuthor VARCHAR ( 50 ) COMMENT '生成功能作者',
	genType CHAR ( 1 ) DEFAULT '0' COMMENT '生成代码方式（0zip压缩包 1自定义路径）',
	genPath VARCHAR ( 200 ) DEFAULT '/' COMMENT '生成路径（不填默认项目路径）',
	OPTIONS VARCHAR ( 1000 ) COMMENT '其它生成选项',
	create_by VARCHAR ( 64 ) DEFAULT '' COMMENT '创建者',
	create_time datetime COMMENT '创建时间',
	update_by VARCHAR ( 64 ) DEFAULT '' COMMENT '更新者',
	update_time datetime COMMENT '更新时间',
	remark VARCHAR ( 500 ) DEFAULT NULL COMMENT '备注',
	dbName VARCHAR ( 100 ) COMMENT '数据库名',
PRIMARY KEY ( tableId ) 
) ENGINE = INNODB auto_increment = 1 COMMENT = '代码生成业务表';


-- ----------------------------
-- 19、代码生成业务表字段
-- ----------------------------
drop table if exists gen_table_column;
CREATE TABLE `gen_table_column`  (
  `columnId` bigint(20) NOT NULL AUTO_INCREMENT COMMENT '编号',
  `tableName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '表名',
  `tableId` bigint(20) NULL DEFAULT NULL COMMENT '归属表编号',
  `columnName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '列名称',
  `columnComment` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '列描述',
  `columnType` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '列类型',
  `csharpType` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT 'C#类型',
  `csharpField` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT 'C#字段名',
  `isPk` tinyint(1) NULL DEFAULT NULL COMMENT '是否主键（1是）',
  `isIncrement` tinyint(1) NULL DEFAULT NULL COMMENT '是否自增（1是）',
  `isRequired` tinyint(1) NULL DEFAULT NULL COMMENT '是否必填（1是）',
  `isInsert` tinyint(1) NULL DEFAULT NULL COMMENT '是否为插入字段（1是）',
  `isEdit` tinyint(1) NULL DEFAULT NULL COMMENT '是否编辑字段（1是）',
  `isList` tinyint(1) NULL DEFAULT NULL COMMENT '是否列表字段（1是）',
  `isQuery` tinyint(4) NULL DEFAULT NULL COMMENT '是否查询字段（1是）',
  `isSort` tinyint(4) NULL DEFAULT NULL COMMENT '是否排序字段（1是）',
  `isExport` tinyint(4) NULL DEFAULT NULL COMMENT '是否导出字段（1是）',
  `queryType` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT 'EQ' COMMENT '查询方式（等于、不等于、大于、小于、范围）',
  `htmlType` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '显示类型（文本框、文本域、下拉框、复选框、单选框、日期控件）',
  `dictType` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '字典类型',
  `sort` int(11) NULL DEFAULT NULL COMMENT '排序',
  `create_by` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '创建者',
  `create_time` datetime(0) NULL DEFAULT NULL COMMENT '创建时间',
  `update_by` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT '' COMMENT '更新者',
  `update_time` datetime(0) NULL DEFAULT NULL COMMENT '更新时间',
  `remark` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL,
  `autoFillType` int(11) NULL DEFAULT NULL COMMENT '自动填充类型',
  PRIMARY KEY (`columnId`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 63 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '代码生成业务表字段' ROW_FORMAT = Dynamic;

-- ----------------------------
-- 代码生成测试
-- Table structure for gen_demo
-- ----------------------------
DROP TABLE IF EXISTS `gen_demo`;
CREATE TABLE `gen_demo`  (
  `id` int(11) NOT NULL AUTO_INCREMENT COMMENT '自增id',
  `name` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '名称',
  `icon` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '图片',
  `showStatus` int(4) NOT NULL COMMENT '显示状态',
  `addTime` datetime(0) NULL DEFAULT NULL COMMENT '添加时间',
  `sex` int(4) NULL DEFAULT NULL COMMENT '用户性别',
  `sort` int(4) NULL DEFAULT 0 COMMENT '排序',
  `remark` VARCHAR(200) COMMENT '备注',
  `beginTime` datetime(0) NULL DEFAULT NULL COMMENT '开始时间',
  `endTime` datetime(0) NULL DEFAULT NULL COMMENT '结束时间',
  `feature` varchar(100) NULL COMMENT '特征',
  PRIMARY KEY (`id`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 10 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for sys_file 文件存储
-- ----------------------------
DROP TABLE IF EXISTS `sys_file`;
CREATE TABLE `sys_file`  (
  `id` BIGINT(11) NOT NULL,
  `realName` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '文件真实名',
  `fileName` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '文件名',
  `fileUrl` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '文件存储地址',
  `storePath` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '仓库位置',
  `accessUrl` varchar(300) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '访问路径',
  `fileSize` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '文件大小',
  `fileType` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '文件类型',
  `fileExt` varchar(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '文件扩展名',
  `create_by` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '创建人',
  `create_time` datetime(0) NULL DEFAULT NULL COMMENT '上传时间',
  `storeType` int(4) NULL DEFAULT NULL COMMENT '存储类型',
  PRIMARY KEY (`id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci ROW_FORMAT = Dynamic;


-- ----------------------------
-- Table structure for sys_common_lang
-- ----------------------------
DROP TABLE IF EXISTS `sys_common_lang`;
CREATE TABLE `sys_common_lang`  (
  `id` bigint(20) NOT NULL COMMENT 'id',
  `lang_code` varchar(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '语言code eg：zh-cn',
  `lang_key` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '翻译key值',
  `lang_name` varchar(2000) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '翻译内容',
  `addtime` datetime(0) NULL DEFAULT NULL COMMENT '添加时间',
  PRIMARY KEY (`Id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci ROW_FORMAT = Dynamic;



-- ----------------------------
-- Table structure for SqlDiffLog
-- ----------------------------
DROP TABLE IF EXISTS `SqlDiffLog`;
CREATE TABLE `SqlDiffLog`  (
  `PId` bigint(20) NOT NULL COMMENT '主键',
  `TableName` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '表名',
  `BusinessData` varchar(4000) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '业务数据内容',
  `DiffType` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '差异类型insert，update，delete',
  `Sql` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL COMMENT '执行sql语句',
  `BeforeData` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL COMMENT '变更前数据',
  `AfterData` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL COMMENT '变更后数据',
  `UserName` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '操作用户名',
  `AddTime` datetime NULL DEFAULT NULL,
  `ConfigId` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '数据库配置id',
  PRIMARY KEY (`PId`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '数据差异日志' ROW_FORMAT = Dynamic;


-- ----------------------------
-- Table structure for email_log
-- ----------------------------
DROP TABLE IF EXISTS `email_log`;
CREATE TABLE `email_log`  (
  `Id` bigint(20) NOT NULL,
  `FromEmail` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '发送邮箱',
  `Subject` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '邮件主题',
  `ToEmails` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL COMMENT '接收邮箱',
  `EmailContent` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL COMMENT '邮件内容',
  `AddTime` datetime NULL DEFAULT NULL COMMENT '发送时间',
  `IsSend` int(11) NULL DEFAULT NULL,
  `SendResult` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL,
  `FileUrl` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL,
  `SendTime` datetime NULL DEFAULT NULL,
  PRIMARY KEY (`Id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '邮件发送记录' ROW_FORMAT = Dynamic;


-- ----------------------------
-- Table structure for emailTpl
-- ----------------------------
DROP TABLE IF EXISTS `emailTpl`;
CREATE TABLE `emailTpl`  (
  `Id` int(11) NOT NULL AUTO_INCREMENT,
  `Name` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL,
  `Content` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL COMMENT '模板内容',
  `Create_by` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '创建人',
  `Create_time` datetime NULL DEFAULT NULL COMMENT '创建时间',
  `Update_by` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '更新人',
  `Update_time` datetime NULL DEFAULT NULL COMMENT '更新时间',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL,
  PRIMARY KEY (`Id`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 2 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '邮件发送模板' ROW_FORMAT = Dynamic;


-- ----------------------------
-- Table structure for smsCode_log
-- ----------------------------
DROP TABLE IF EXISTS `smsCode_log`;
CREATE TABLE `smsCode_log`  (
  `Id` bigint(20) NOT NULL,
  `SmsCode` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '短信验证码',
  `Userid` bigint(20) NULL DEFAULT NULL COMMENT '用户id',
  `PhoneNum` bigint(20) NULL DEFAULT NULL COMMENT '手机号',
  `SmsContent` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '短信内容',
  `AddTime` datetime NULL DEFAULT NULL COMMENT '发送时间',
  `UserIP` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '用户IP',
  `SendType` int(11) NULL DEFAULT NULL COMMENT '1、登录 2、注册 3、找回密码',
  `Location` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '地理位置',
  PRIMARY KEY (`Id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '短信验证码记录' ROW_FORMAT = Dynamic;

DROP TABLE IF EXISTS `article_topic`;
 CREATE TABLE `article_topic` (
  `topicId` int NOT NULL,
  `topicName` varchar(20) NOT NULL,
  `topicDescription` text,
  `joinNum` int DEFAULT NULL,
  `viewNum` int DEFAULT NULL,
  `addTime` datetime DEFAULT NULL,
  `topicType` int DEFAULT NULL,
  PRIMARY KEY (`topicId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;