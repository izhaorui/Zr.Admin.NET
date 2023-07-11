
-- ----------------------------
-- Records of sys_dept
-- ----------------------------
INSERT INTO `sys_dept` VALUES (100, 0,   '0',      'A公司', 0, 'zr', '', '', 0, 0, 'admin', NULL, '', NULL, NULL);
INSERT INTO `sys_dept` VALUES (101, 100, '0,100', '研发部门', 1, 'zr', '', '', 0, 0, 'admin', NULL, '', NULL, NULL);
INSERT INTO `sys_dept` VALUES (102, 100, '0,100', '市场部门', 2, 'zr', '', '', 0, 0, 'admin', NULL, '', NULL, NULL);
INSERT INTO `sys_dept` VALUES (103, 100, '0,100', '测试部门', 3, 'zr', '', '', 0, 0, 'admin', NULL, '', NULL, NULL);
INSERT INTO `sys_dept` VALUES (104, 100, '0,100', '财务部门', 4, 'zr', '', '', 0, 0, 'admin', NULL, '', NULL, NULL);
INSERT INTO `sys_dept` VALUES (200, 0,   '0',      'B公司', 0, 'zr', '', '', 0, 0, 'admin', NULL, '', NULL, NULL);


-- ----------------------------
-- Records of sys_dict_type
-- ----------------------------
INSERT INTO `sys_dict_type` VALUES (1, '用户性别', 'sys_user_sex', '0', 'Y', 'admin', SYSDATE(), '', NULL, '用户性别列表',NULL);
INSERT INTO `sys_dict_type` VALUES (2, '菜单状态', 'sys_show_hide', '0', 'Y', 'admin', SYSDATE(), '', NULL, '菜单状态列表',NULL);
INSERT INTO `sys_dict_type` VALUES (3, '系统开关', 'sys_normal_disable', '0', 'Y', 'admin', SYSDATE(), '', NULL, '系统开关列表',NULL);
INSERT INTO `sys_dict_type` VALUES (4, '任务状态', 'sys_job_status', '0', 'Y', 'admin', SYSDATE(), '', NULL, '任务状态列表',NULL);
INSERT INTO `sys_dict_type` VALUES (5, '任务分组', 'sys_job_group', '0', 'Y', 'admin', SYSDATE(), '', NULL, '任务分组列表',NULL);
INSERT INTO `sys_dict_type` VALUES (6, '系统是否', 'sys_yes_no', '0', 'Y', 'admin', SYSDATE(), '', NULL, '系统是否列表',NULL);
INSERT INTO `sys_dict_type` VALUES (7, '通知类型', 'sys_notice_type', '0', 'Y', 'admin', SYSDATE(), '', NULL, '通知类型列表',NULL);
INSERT INTO `sys_dict_type` VALUES (8, '通知状态', 'sys_notice_status', '0', 'Y', 'admin', SYSDATE(), '', NULL, '通知状态列表',NULL);
INSERT INTO `sys_dict_type` VALUES (9, '操作类型', 'sys_oper_type', '0', 'Y', 'admin', SYSDATE(), '', NULL, '操作类型列表',NULL);
INSERT INTO `sys_dict_type` VALUES (10, '系统状态', 'sys_common_status', '0', 'Y', 'admin', SYSDATE(), '', NULL, '登录状态列表',NULL);
INSERT INTO `sys_dict_type` VALUES (11, '文章状态', 'sys_article_status', '0', 'Y', 'admin', SYSDATE(), '', NULL, NULL,NULL);
INSERT INTO `sys_dict_type` VALUES (12, '多语言类型', 'sys_lang_type', '0', 'Y', 'admin', SYSDATE(), '', NULL, '多语言字典类型',NULL);


-- ----------------------------
-- Records of sys_dict_data
-- ----------------------------
INSERT INTO `sys_dict_data` VALUES (1, 1, '男', '0', 'sys_user_sex', '', '', 'Y', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '性别男');
INSERT INTO `sys_dict_data` VALUES (2, 2, '女', '1', 'sys_user_sex', '', '', 'N', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '性别女');
INSERT INTO `sys_dict_data` VALUES (3, 3, '未知', '2', 'sys_user_sex', '', '', 'N', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '性别未知');
INSERT INTO `sys_dict_data` VALUES (4, 1, '显示', '0', 'sys_show_hide', '', 'primary', 'Y', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '显示菜单');
INSERT INTO `sys_dict_data` VALUES (5, 2, '隐藏', '1', 'sys_show_hide', '', 'danger', 'N', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '隐藏菜单');
INSERT INTO `sys_dict_data` VALUES (6, 1, '正常', '0', 'sys_normal_disable', '', 'primary', 'Y', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '正常状态');
INSERT INTO `sys_dict_data` VALUES (7, 2, '停用', '1', 'sys_normal_disable', '', 'danger', 'N', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '停用状态');
INSERT INTO `sys_dict_data` VALUES (8, 1, '正常', '0', 'sys_job_status', '', 'primary', 'Y', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '正常状态');
INSERT INTO `sys_dict_data` VALUES (9, 2, '异常', '1', 'sys_job_status', '', 'danger', 'N', '0', 'admin', '2021-02-24 10:56:21', '', '2021-07-02 14:09:09', '停用状态');
INSERT INTO `sys_dict_data` VALUES (10, 1, '默认', 'DEFAULT', 'sys_job_group', '', '', 'Y', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '默认分组');
INSERT INTO `sys_dict_data` VALUES (11, 2, '系统', 'SYSTEM', 'sys_job_group', '', '', 'N', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '系统分组');
INSERT INTO `sys_dict_data` VALUES (12, 1, '是', 'Y', 'sys_yes_no', '', 'primary', 'Y', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '系统默认是');
INSERT INTO `sys_dict_data` VALUES (13, 2, '否', 'N', 'sys_yes_no', '', 'danger', 'N', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '系统默认否');
INSERT INTO `sys_dict_data` VALUES (14, 1, '通知', '1', 'sys_notice_type', '', 'warning', 'Y', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '通知');
INSERT INTO `sys_dict_data` VALUES (15, 2, '公告', '2', 'sys_notice_type', '', 'success', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '公告');
INSERT INTO `sys_dict_data` VALUES (16, 1, '正常', '0', 'sys_notice_status', '', 'primary', 'Y', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '正常状态');
INSERT INTO `sys_dict_data` VALUES (17, 2, '关闭', '1', 'sys_notice_status', '', 'danger', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '关闭状态');
INSERT INTO `sys_dict_data` VALUES (18, 0, '其他', '0', 'sys_oper_type', '', 'info', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '其他操作');
INSERT INTO `sys_dict_data` VALUES (19, 1, '新增', '1', 'sys_oper_type', '', 'info', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '新增操作');
INSERT INTO `sys_dict_data` VALUES (20, 2, '修改', '2', 'sys_oper_type', '', 'info', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '修改操作');
INSERT INTO `sys_dict_data` VALUES (21, 3, '删除', '3', 'sys_oper_type', '', 'danger', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '删除操作');
INSERT INTO `sys_dict_data` VALUES (22, 4, '授权', '4', 'sys_oper_type', '', 'primary', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '授权操作');
INSERT INTO `sys_dict_data` VALUES (23, 5, '导出', '5', 'sys_oper_type', '', 'warning', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '导出操作');
INSERT INTO `sys_dict_data` VALUES (24, 6, '导入', '6', 'sys_oper_type', '', 'warning', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '导入操作');
INSERT INTO `sys_dict_data` VALUES (25, 7, '强退', '7', 'sys_oper_type', '', 'danger', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '强退操作');
INSERT INTO `sys_dict_data` VALUES (26, 8, '生成代码', '8', 'sys_oper_type', '', 'warning', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '生成操作');
INSERT INTO `sys_dict_data` VALUES (27, 9, '清空数据', '9', 'sys_oper_type', '', 'danger', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '清空操作');
INSERT INTO `sys_dict_data` VALUES (28, 1, '成功', '0', 'sys_common_status', '', 'primary', 'N', '0', 'admin', '2021-02-24 10:56:23', '', NULL, '正常状态');
INSERT INTO `sys_dict_data` VALUES (29, 2, '失败', '1', 'sys_common_status', '', 'danger', 'N', '0', 'admin', '2021-02-24 10:56:23', '', NULL, '停用状态');
INSERT INTO `sys_dict_data` VALUES (30, 1, '发布', '1', 'sys_article_status', NULL, NULL, NULL, '0', 'admin', '2021-08-19 10:34:56', '', NULL, NULL);
INSERT INTO `sys_dict_data` VALUES (31, 2, '草稿', '2', 'sys_article_status', NULL, NULL, NULL, '0', 'admin', '2021-08-19 10:35:06', '', NULL, NULL);

INSERT INTO `sys_dict_data` VALUES (32, 1, '中文', 'zh-cn',  'sys_lang_type', NULL, NULL, NULL, '0', 'admin', '2021-08-19 10:35:06', '', NULL, NULL);
INSERT INTO `sys_dict_data` VALUES (33, 2, '英文', 'en',     'sys_lang_type', NULL, NULL, NULL, '0', 'admin', '2021-08-19 10:35:06', '', NULL, NULL);
INSERT INTO `sys_dict_data` VALUES (34, 3, '繁体', 'zh-tw',  'sys_lang_type', NULL, NULL, NULL, '0', 'admin', '2021-08-19 10:35:06', '', NULL, NULL);


-- ----------------------------
-- Records of sys_menu
-- ----------------------------
-- 一级菜单
INSERT INTO sys_menu VALUES (1, '系统管理', 0, 1, 'system', NULL, 0, 0, 'M', '0', '0', '', 'system', '', SYSDATE(), '', NULL, '系统管理目录', 'menu.system');
INSERT INTO sys_menu VALUES (2, '系统监控', 0, 2, 'monitor', NULL, 0, 0, 'M', '0', '0', '', 'monitor', '', SYSDATE(), '', NULL, '系统监控目录', 'menu.monitoring');
INSERT INTO sys_menu VALUES (3, '系统工具', 0, 3, 'tool', NULL, 0, 0, 'M', '0', '0', '', 'tool', '', SYSDATE(), '', NULL, '系统工具目录', 'menu.systemTools');
INSERT INTO sys_menu VALUES (5, '官网地址', 0, 5, 'http://www.izhaorui.cn/doc', NULL, 1, 0, 'M', '0', '0', '', 'link', '', SYSDATE(), '', NULL, 'Zr官网地址', 'menu.officialWebsite');
INSERT INTO sys_menu VALUES (6, '控制台',   0, 0, 'dashboard', 'index_v1', 0, 0, 'C', '0', '0', '', 'dashboard', '', SYSDATE(), '', NULL, '', 'menu.dashboard');

-- 二级菜单
INSERT INTO sys_menu VALUES (100, '用户管理', 1, 1, 'user', 'system/user/index', 0, 0, 'C', '0', '0', 'system:user:list', 'user', '', SYSDATE(), '', NULL, '用户管理菜单', 'menu.systemUser');
INSERT INTO sys_menu VALUES (101, '角色管理', 1, 2, 'role', 'system/role/index', 0, 0, 'C', '0', '0', 'system:role:list', 'peoples', '', SYSDATE(), '', NULL, '角色管理菜单', 'menu.systemRole');
INSERT INTO sys_menu VALUES (102, '菜单管理', 1, 3, 'menu', 'system/menu/index', 0, 0, 'C', '0', '0', 'system:menu:list', 'tree-table', '', SYSDATE(), '', NULL, '菜单管理菜单', 'menu.systemMenu');
INSERT INTO sys_menu VALUES (103, '部门管理', 1, 4, 'dept', 'system/dept/index', 0, 0, 'C', '0', '0', 'system:dept:list', 'tree', '', SYSDATE(), '', NULL, '部门管理菜单', 'menu.systemDept');
INSERT INTO sys_menu VALUES (104, '岗位管理', 1, 5, 'post', 'system/post/index', 0, 0, 'C', '0', '0', 'system:post:list', 'post', '', SYSDATE(), '', NULL, '岗位管理菜单', 'menu.systemPost');
INSERT INTO sys_menu VALUES (105, '字典管理', 1, 6, 'dict', 'system/dict/index', 0, 0, 'C', '0', '0', 'system:dict:list', 'dict', '', SYSDATE(), '', NULL, '', 'menu.systemDic');
INSERT INTO sys_menu VALUES (106, '角色分配', 1, 2, 'roleusers', 'system/roleusers/index', 0, 0, 'C', '1', '0', 'system:roleusers:list', 'people', '', SYSDATE(), '', NULL, NULL, '');
INSERT into sys_menu VALUES (107, '参数设置', 1, 8, 'config','system/config/index', 0, 0, 'C', '0', '0', 'system:config:list','edit', '', SYSDATE(), '', NULL, '', 'menu.systemParam');
INSERT INTO sys_menu VALUES (108, '日志管理', 1, 10, 'log', ''                     , 0, 0, 'M', '0', '0', '', 'log', '', SYSDATE(), '', NULL, '日志管理菜单', 'menu.systemLog');
INSERT INTO sys_menu VALUES (109, '通知公告', 1, 9, 'notice', 'system/notice/index', 0, 0, 'C', '0', '0', 'system:notice:list', 'message', '', SYSDATE(), '', NULL, '通知公告菜单', 'menu.systemNotice');
INSERT INTO sys_menu VALUES (110, '定时任务', 2, 10, 'job', 'monitor/job/index', 0, 0, 'C', '0', '0', '', 'job', '', SYSDATE(), '', NULL, '定时任务菜单', 'menu.timedTask');
INSERT INTO sys_menu VALUES (111, '在线用户', 2, 10, 'onlineusers', 'monitor/onlineuser/index', 0, 0, 'C', '0', '0', '', 'online', '', SYSDATE(), '', NULL, '在线用户', 'layout.onlineUsers');
INSERT INTO sys_menu VALUES (112, '服务监控', 2, 11, 'server', 'monitor/server/index', 0, 0, 'C', '0', '0', 'monitor:server:list', 'server', '', SYSDATE(), '', NULL, '服务监控菜单', 'menu.serviceMonitor');
INSERT INTO sys_menu VALUES (113, '缓存监控', 2, 12, 'cache', 'monitor/cache/index', 0, 0, 'C', '1', '1', 'monitor:cache:list', 'redis', '', SYSDATE(), '', NULL, '缓存监控菜单', 'menu.cacheMonitor');

INSERT INTO sys_menu VALUES (114, '表单构建', 3, 13, 'build', 'tool/build/index', 0, 0, 'C', '0', '0', 'tool:build:list', 'build', '', SYSDATE(), '', NULL, '表单构建菜单', 'menu.formBuild');
INSERT INTO sys_menu VALUES (115, '代码生成', 3, 14, 'gen', 'tool/gen/index', 0, 0, 'C', '0', '0', 'tool:gen:list', 'code', '', SYSDATE(), '', NULL, '代码生成菜单', 'menu.codeGeneration');
INSERT INTO sys_menu VALUES (116, '系统接口', 3, 15, 'swagger', 'tool/swagger/index', 0, 0, 'C', '0', '0', 'tool:swagger:list', 'swagger', '', SYSDATE(), '', NULL, '系统接口菜单', 'menu.systemInterface');
INSERT INTO sys_menu VALUES (117, '发送邮件', 3, 16, 'sendEmail', 'tool/email/sendEmail', 0, 0, 'C', '0', '0', 'tool:email:send', 'email', '', SYSDATE(), '', NULL, '发送邮件菜单', 'menu.sendEmail');
INSERT INTO sys_menu VALUES (118, '文章管理', 3, 18, 'article', NULL, 0, 0, 'M', '0', '0', NULL, 'documentation', '', SYSDATE(), '', NULL, NULL, 'menu.systemArticle');


INSERT INTO sys_menu VALUES (1047, '发布文章', 3, 2, '/article/publish', 'system/article/publish', 0, 0, 'C', '1', '0', 'system:article:publish', 'log', '', SYSDATE(), '', NULL, NULL, '');
INSERT INTO sys_menu VALUES (119, '文章列表', 118, 1, 'index', 'system/article/manager', 0, 0, 'C', '0', '0', 'system:article:list', 'list', '', SYSDATE(), '', NULL, NULL, 'menu.articleList');
-- 三级菜单日志管理
INSERT INTO sys_menu VALUES (500, '操作日志', 108, 1, 'operlog', 'monitor/operlog/index', 0, 0, 'C', '0', '0', 'monitor:operlog:list', 'form', '', SYSDATE(), '', NULL, '操作日志菜单', 'menu.operLog');
INSERT INTO sys_menu VALUES (501, '登录日志', 108, 2, 'logininfor', 'monitor/logininfor/index', 0, 0, 'C', '0', '0', 'monitor:logininfor:list', 'logininfor', '', SYSDATE(), '', NULL, '登录日志菜单', 'menu.loginLog');


-- 用户管理 按钮
INSERT INTO sys_menu VALUES (1001, '用户查询', 100, 1, '', '', 0, 0, 'F', '0', '0', 'system:user:query', '', '', SYSDATE(), '', NULL, '', NULL);
INSERT INTO sys_menu VALUES (1002, '用户添加', 100, 2, '', '', 0, 0, 'F', '0', '0', 'system:user:add', '', '', SYSDATE(), '', NULL, '', NULL);
INSERT INTO sys_menu VALUES (1003, '用户修改', 100, 3, '', '', 0, 0, 'F', '0', '0', 'system:user:edit', '', '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1004, '用户删除', 100, 4, '', '', 0, 0, 'F', '0', '0', 'system:user:remove', '', '', SYSDATE(), '', NULL, '', NULL);
INSERT INTO sys_menu VALUES (1005, '用户导出', 100, 5, '', '', 0, 0, 'F', '0', '0', 'system:user:export', '#', '', SYSDATE(), '', NULL, '', NULL);
INSERT INTO sys_menu VALUES (1006, '用户导入', 100, 6, '', '', 0, 0, 'F', '0', '0', 'system:user:import', '#', '', SYSDATE(), '', NULL, '', NULL);
INSERT INTO sys_menu VALUES (1007, '重置密码', 100, 7, '', '', 0, 0, 'F', '0', '0', 'system:user:resetPwd', '#', '', SYSDATE(), '', NULL, '', NULL);
-- 权限管理 按钮
INSERT INTO sys_menu VALUES (1008, '角色查询', 101, 1, '', '', 0, 0, 'F', '0', '0', 'system:role:query', '#', '', SYSDATE(), '', NULL, '', NULL);
INSERT INTO sys_menu VALUES (1009, '角色新增', 101, 2, '', '', 0, 0, 'F', '0', '0', 'system:role:add', '#', '', SYSDATE(), '', NULL, '', NULL);
INSERT INTO sys_menu VALUES (1010, '角色修改', 101, 3, '', '', 0, 0, 'F', '0', '0', 'system:role:edit', '#', '', SYSDATE(), '', NULL, '', NULL);
INSERT INTO sys_menu VALUES (1011, '角色删除', 101, 4, '', '', 0, 0, 'F', '0', '0', 'system:role:remove', '#', '', SYSDATE(), '', NULL, '', NULL);
INSERT INTO sys_menu VALUES (1012, '角色授权', 101, 5, '', '', 0, 0, 'F', '0', '0', 'system:role:authorize', '#', '', SYSDATE(), '', NULL, '', NULL);
-- 分配用户 按钮
INSERT INTO sys_menu VALUES (1029, '新增用户', 106, 2, '', '', 0, 0, 'F', '0', '0', 'system:roleusers:add', NULL, '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1030, '删除用户', 106, 3, '', '', 0, 0, 'F', '0', '0', 'system:roleusers:remove', NULL, '', SYSDATE(), '', NULL, NULL, NULL);
-- 菜单管理 按钮
INSERT INTO sys_menu VALUES (1013, '菜单查询', 102, 1, '', '', 0, 0, 'F', '0', '0', 'system:menu:query', '#', '', SYSDATE(), '', NULL, '', NULL);
INSERT INTO sys_menu VALUES (1014, '菜单新增', 102, 2, '', '', 0, 0, 'F', '0', '0', 'system:menu:add', '#', '', SYSDATE(), '', NULL, '', NULL);
INSERT INTO sys_menu VALUES (1015, '菜单修改', 102, 3, '', '', 0, 0, 'F', '0', '0', 'system:menu:edit', '#', '', SYSDATE(), '', NULL, '', NULL);
INSERT INTO sys_menu VALUES (1016, '菜单删除', 102, 4, '', '', 0, 0, 'F', '0', '0', 'system:menu:remove', '#', '', SYSDATE(), '', NULL, '', NULL);
INSERT INTO sys_menu VALUES (1017, '修改排序', 102, 5, '', '', 0, 0, 'F', '0', '0', 'system:menu:changeSort', '', '', SYSDATE(), '', NULL, NULL, NULL);
-- 部门管理 按钮
INSERT INTO sys_menu VALUES (1018, '部门查询', 103, 1, '', '', 0, 0, 'F', '0', '0', 'system:dept:query', '', '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1019, '部门新增', 103, 2, '', '', 0, 0, 'F', '0', '0', 'system:dept:add', NULL, '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1020, '部门修改', 103, 3, '', '', 0, 0, 'F', '0', '0', 'system:dept:update', NULL, '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1021, '部门删除', 103, 4, '', '', 0, 0, 'F', '0', '0', 'system:dept:remove', NULL, '', SYSDATE(), '', NULL, NULL, NULL);
-- 岗位管理 按钮
INSERT INTO sys_menu VALUES (1022, '岗位查询', 104, 1, '', '', 0, 0, 'F', '0', '0', 'system:post:list', NULL, '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1023, '岗位添加', 104, 2, '', '', 0, 0, 'F', '0', '0', 'system:post:add', NULL, '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1024, '岗位删除', 104, 3, '', '', 0, 0, 'F', '0', '0', 'system:post:remove', NULL, '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1025, '岗位编辑', 104, 4, '', '', 0, 0, 'F', '0', '0', 'system:post:edit', '', '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1070, '岗位导出', 104, 4, '', '', 0, 0, 'F', '0', '0', 'system:post:export', '', '', SYSDATE(), '', NULL, NULL, NULL);
-- 字典管理 按钮
INSERT INTO sys_menu VALUES (1031, '字典查询', 105, 1, '', '', 0, 0, 'F', '0', '0', 'system:dict:query', NULL, '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1026, '字典新增', 105, 1, '', '', 0, 0, 'F', '0', '0', 'system:dict:add', NULL, '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1027, '字典修改', 105, 2, '', '', 0, 0, 'F', '0', '0', 'system:dict:edit', NULL, '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1028, '字典删除', 105, 3, '', '', 0, 0, 'F', '0', '0', 'system:dict:remove', NULL, '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1071, '字典导出', 105, 3, '', '', 0, 0, 'F', '0', '0', 'system:dict:export', NULL, '', SYSDATE(), '', NULL, NULL, NULL);

-- 定时任务 按钮
INSERT INTO sys_menu values (1032, '任务查询', 110, 1, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:job:list', '#', '', sysdate(), '', null, '', NULL);
INSERT INTO sys_menu VALUES (1033, '任务新增', 110, 2, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:job:add', '', '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1034, '任务删除', 110, 3, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:job:delete', '', '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1035, '任务修改', 110, 4, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:job:edit', '', '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1036, '任务启动', 110, 5, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:job:start', '', '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1037, '任务运行', 110, 7, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:job:run', NULL, '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1038, '任务停止', 110, 8, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:job:stop', NULL, '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1039, '任务日志', 2, 0, 'job/log', 'monitor/job/log', 0, 0, 'C', '1', '0', 'monitor:job:query', 'log', '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1040, '任务导出', 110, 10, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:job:export', NULL, '', SYSDATE(), '', NULL, NULL, NULL);
-- 操作日志 按钮
INSERT INTO sys_menu values (1041, '操作查询', 500, 1, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:operlog:query', '', '', sysdate(), '', null, NULL, NULL);
INSERT INTO sys_menu VALUES (1042, '操作删除', 500, 2, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:operlog:remove', '', '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1043, '操作日志导出', 500,3, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:operlog:export', '', '', SYSDATE(), '', NULL, NULL, NULL);
-- 登录日志 按钮
INSERT INTO sys_menu values (1044, '登录查询', 501, 1, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:logininfor:query',  '', '', sysdate(), '', null, NULL, NULL);
INSERT INTO sys_menu VALUES (1045, '登录删除', 501, 1, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:logininfor:remove', '', '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1046, '登录日志导出', 501, 1, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:logininfor:export', '', '', SYSDATE(), '', NULL, NULL, NULL);
-- 文章管理 按钮
INSERT INTO sys_menu VALUES (1048, '文章新增', 118, 3, '#', NULL, 0, 0, 'F', '0', '0', 'system:article:add', '', '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1049, '文章修改', 118, 4, '#', NULL, 0, 0, 'F', '0', '0', 'system:article:update', '', '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1050, '文章删除', 118, 5, '#', NULL, 0, 0, 'F', '0', '0', 'system:article:delete', '', '', SYSDATE(), '', NULL, NULL, NULL);
-- 通知公告 按钮
INSERT INTO sys_menu VALUES (1051, '查询公告', 109, 1, '#', NULL, 0, 0, 'F', '0', '0', 'system:notice:query', '', '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1052, '新增公告', 109, 2, '#', NULL, 0, 0, 'F', '0', '0', 'system:notice:add', '', '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1053, '删除公告', 109, 3, '#', NULL, 0, 0, 'F', '0', '0', 'system:notice:delete', '', '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1054, '修改公告', 109, 4, '#', NULL, 0, 0, 'F', '0', '0', 'system:notice:update', '', '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1055, '导出公告', 109, 5, '#', NULL, 0, 0, 'F', '0', '0', 'system:notice:export', '', '', SYSDATE(), '', NULL, NULL, NULL);
-- 代码生成 按钮
INSERT INTO sys_menu VALUES (1060, '生成修改', 3, 1, '/gen/editTable', 'tool/gen/editTable', 0, 0, 'C', '1', '0', 'tool:gen:edit', '', '', SYSDATE(), '', NULL, NULL, NULL);
insert into sys_menu VALUES (1061, '生成查询', 115, 1, '#', NULL, 0, 0, 'F', '0', '0', 'tool:gen:query',  '', '', sysdate(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1062, '生成删除', 115, 1, '#', NULL, 0, 0, 'F', '0', '0', 'tool:gen:remove', '', '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1063, '导入代码', 115, 1, '#', NULL, 0, 0, 'F', '0', '0', 'tool:gen:import', '', '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1064, '生成代码', 115, 1, '#', NULL, 0, 0, 'F', '0', '0', 'tool:gen:code', '', '', SYSDATE(), '', NULL, NULL, NULL);
INSERT INTO sys_menu VALUES (1065, '预览代码', 115, 1, '#', NULL, 0, 0, 'F', '0', '0', 'tool:gen:preview', '', '', SYSDATE(), '', NULL, NULL, NULL);


-- 文件存储菜单
INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by, create_time, remark, menuName_key) 
VALUES ('文件存储', 3, 17, 'file', 'tool/file/index', 0, 0, 'C', '0', '0', 'tool:file:list', 'upload', '', sysdate(), '文件存储菜单', 'menu.fileStorage');

-- 按钮父菜单id
SELECT @fileMenuId := LAST_INSERT_ID();

INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_time) 
VALUES ('查询', @fileMenuId, 1, '#', NULL, 0, 0, 'F', '0', '0', 'tool:file:query', '', sysdate());

INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_time) 
VALUES ('新增', @fileMenuId, 2, '#', NULL, 0, 0, 'F', '0', '0', 'tool:file:add', '',  sysdate());

INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_time) 
VALUES ('删除', @fileMenuId, 3, '#', NULL, 0, 0, 'F', '0', '0', 'tool:file:delete', '', sysdate());

INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_time) 
VALUES ('修改', @fileMenuId, 4, '#', NULL, 0, 0, 'F', '0', '0', 'tool:file:update', '', sysdate());

INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_time) 
VALUES ('导出', @fileMenuId, 5, '#', NULL, 0, 0, 'F', '0', '0', 'tool:file:export', '', sysdate());

-- 多语言配置菜单
INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by, create_time, menuName_key) 
VALUES ('多语言配置', 1, 20, 'CommonLang', 'system/commonLang/index', 0, 0, 'C', '0', '0', 'system:lang:list', 'language', 'system', sysdate(), 'menu.systemLang');

SELECT @langMenuId := LAST_INSERT_ID();

INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by, create_time) 
VALUES ('查询', @langMenuId, 1, '#', NULL, 0, 0, 'F', '0', '0', 'system:lang:query', '', 'system', sysdate());

INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by, create_time) 
VALUES ('新增', @langMenuId, 2, '#', NULL, 0, 0, 'F', '0', '0', 'system:lang:add', '', 'system',  sysdate());

INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by, create_time) 
VALUES ('删除', @langMenuId, 3, '#', NULL, 0, 0, 'F', '0', '0', 'system:lang:delete', '', 'system', sysdate());

INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by, create_time) 
VALUES ('修改', @langMenuId, 4, '#', NULL, 0, 0, 'F', '0', '0', 'system:lang:edit', '', 'system', sysdate());


-- 文章目录菜单
INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by, create_time) 
VALUES ('文章目录', 118, 999, 'ArticleCategory', 'system/article/articleCategory', 0, 0, 'C', '0', '0', 'articlecategory:list', 'tree-table', 'system', sysdate());
-- 按钮父菜单id
SELECT @cmenuId := LAST_INSERT_ID();

INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by, create_time) 
VALUES ('查询', @cmenuId, 1, '#', NULL, 0, 0, 'F', '0', '0', 'articlecategory:query', '', 'system', sysdate());

INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by, create_time) 
VALUES ('新增', @cmenuId, 2, '#', NULL, 0, 0, 'F', '0', '0', 'articlecategory:add', '', 'system',  sysdate());

INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by, create_time) 
VALUES ('删除', @cmenuId, 3, '#', NULL, 0, 0, 'F', '0', '0', 'articlecategory:delete', '', 'system', sysdate());

INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by, create_time) 
VALUES ('修改', @cmenuId, 4, '#', NULL, 0, 0, 'F', '0', '0', 'articlecategory:edit', '', 'system', sysdate());

-- ----------------------------
-- Records of sys_post
-- ----------------------------
INSERT INTO `sys_post` VALUES (1, 'CEO', '董事长', 1, '0', '', SYSDATE(), '', NULL, '');
INSERT INTO `sys_post` VALUES (2, 'SE', '项目经理', 2, '0', '', SYSDATE(), '', NULL, '');
INSERT INTO `sys_post` VALUES (3, 'HR', '人力资源', 3, '0', '', SYSDATE(), '', NULL, '');
INSERT INTO `sys_post` VALUES (4, 'USER', '普通员工', 4, '0', '', SYSDATE(), '', NULL, '');
INSERT INTO `sys_post` VALUES (6, 'PM', '人事经理', 0, '0', NULL, SYSDATE(), '', NULL, NULL);
INSERT INTO `sys_post` VALUES (7, 'GM', '总经理', 0, '0', NULL, SYSDATE(), '', NULL, NULL);
INSERT INTO `sys_post` VALUES (8, 'COO', '首席运营官', 0, '0', NULL, SYSDATE(), '', NULL, NULL);
INSERT INTO `sys_post` VALUES (9, 'CFO', '首席财务官', 0, '0', NULL, SYSDATE(), '', NULL, NULL);
INSERT INTO `sys_post` VALUES (10, 'CTO', '首席技术官', 0, '0', NULL, SYSDATE(), '', NULL, NULL);
INSERT INTO `sys_post` VALUES (11, 'HRD', '人力资源总监', 0, '0', NULL, SYSDATE(), '', NULL, NULL);
INSERT INTO `sys_post` VALUES (12, 'VP', '副总裁', 0, '0', NULL, SYSDATE(), '', NULL, NULL);
INSERT INTO `sys_post` VALUES (13, 'OD', '运营总监', 0, '0', NULL, SYSDATE(), '', NULL, NULL);
INSERT INTO `sys_post` VALUES (14, 'MD', '市场总监', 0, '0', NULL, SYSDATE(), '', NULL, NULL);

-- ----------------------------
-- Records of sys_role
-- ----------------------------
INSERT INTO `sys_role` VALUES (1, '超级管理员', 'admin', 1, 1, 1, 0, 0, 0, 'admin', SYSDATE(), 'system', NULL, '超级管理员');
INSERT INTO `sys_role` VALUES (2, '普通角色', 'common', 2, 2, 1, 0, 0, 0, 'admin', SYSDATE(), 'system', NULL, '普通角色');

-- ----------------------------
-- Records of sys_role_dept
-- ----------------------------
INSERT INTO `sys_role_dept` VALUES (2, 100);
INSERT INTO `sys_role_dept` VALUES (2, 101);
INSERT INTO `sys_role_dept` VALUES (2, 105);

-- ----------------------------
-- Records of sys_role_menu
-- ----------------------------

INSERT INTO `sys_role_menu` VALUES (2, 1, NULL, SYSDATE());
INSERT INTO `sys_role_menu` VALUES (2, 3, NULL, SYSDATE());
INSERT INTO `sys_role_menu` VALUES (2, 5, NULL, SYSDATE());
INSERT INTO `sys_role_menu` VALUES (2, 6, NULL, SYSDATE());

INSERT INTO `sys_role_menu` VALUES (2, 100, 'admin', SYSDATE());
INSERT INTO `sys_role_menu` VALUES (2, 101, 'admin', SYSDATE());
INSERT INTO `sys_role_menu` VALUES (2, 102, 'admin', SYSDATE());
INSERT INTO `sys_role_menu` VALUES (2, 103, 'admin', SYSDATE());
INSERT INTO `sys_role_menu` VALUES (2, 104, 'admin', SYSDATE());
INSERT INTO `sys_role_menu` VALUES (2, 106, 'admin', SYSDATE());
INSERT INTO `sys_role_menu` VALUES (2, 108, 'admin', SYSDATE());
INSERT INTO `sys_role_menu` VALUES (2, 109, 'admin', SYSDATE());
INSERT INTO `sys_role_menu` VALUES (2, 114, 'admin', SYSDATE());
INSERT INTO `sys_role_menu` VALUES (2, 500, 'admin', SYSDATE());
INSERT INTO `sys_role_menu` VALUES (2, 501, 'admin', SYSDATE());
INSERT INTO `sys_role_menu` VALUES (2, 1001, 'admin', SYSDATE());
INSERT INTO `sys_role_menu` VALUES (2, 1008, 'admin', SYSDATE());
INSERT INTO `sys_role_menu` VALUES (2, 1013, 'admin', SYSDATE());
INSERT INTO `sys_role_menu` VALUES (2, 1018, 'admin', SYSDATE());
INSERT INTO `sys_role_menu` VALUES (2, 1022, 'admin', SYSDATE());
INSERT INTO `sys_role_menu` VALUES (2, 1031, 'admin', SYSDATE());
INSERT INTO `sys_role_menu` VALUES (2, 1044, 'admin', SYSDATE());
INSERT INTO `sys_role_menu` VALUES (2, 1051, 'admin', SYSDATE());

-- 编辑者
INSERT INTO `sys_role_menu` VALUES (3, 4   , NULL, SYSDATE());
INSERT INTO `sys_role_menu` VALUES (3, 118 , NULL, SYSDATE());
INSERT INTO `sys_role_menu` VALUES (3, 1047, NULL, SYSDATE());
INSERT INTO `sys_role_menu` VALUES (3, 1048, NULL, SYSDATE());
INSERT INTO `sys_role_menu` VALUES (3, 1049, NULL, SYSDATE());
INSERT INTO `sys_role_menu` VALUES (3, 1050, NULL, SYSDATE());

-- ----------------------------
-- Records of sys_user
-- ----------------------------
INSERT INTO `sys_user`(`userId`, `deptId`, `userName`, `nickName`, `userType`, `email`, `phonenumber`, `sex`, `avatar`, `password`, `status`, `delFlag`, `loginIP`, `loginDate`, `create_by`, `create_time`, `update_by`, `update_time`, `remark`) VALUES (1, 0, 'admin', '管理员', '0', '', '', '0', '', 'e10adc3949ba59abbe56e057f20f883e', '0', '0', '127.0.0.1', '2021-08-23 14:03:17', 'admin', '2020-11-26 11:52:59', 'admin', '2021-08-03 10:11:24', '管理员');
INSERT INTO `sys_user`(`userId`, `deptId`, `userName`, `nickName`, `userType`, `email`, `phonenumber`, `sex`, `avatar`, `password`, `status`, `delFlag`, `loginIP`, `loginDate`, `create_by`, `create_time`, `update_by`, `update_time`, `remark`) VALUES (2, 0, 'user', '普通用户', '0', NULL, NULL, '0', '', 'e10adc3949ba59abbe56e057f20f883e', '0', '0', '', '0001-01-01 00:00:00', 'admin', '2021-07-05 17:29:13', 'admin', '2021-08-02 16:53:04', '普通用户');
INSERT INTO `sys_user`(`userId`, `deptId`, `userName`, `nickName`, `userType`, `email`, `phonenumber`, `sex`, `avatar`, `password`, `status`, `delFlag`, `loginIP`, `loginDate`, `create_by`, `create_time`, `update_by`, `update_time`, `remark`) VALUES (3, 100, 'editor', '编辑人员', '0', NULL, NULL, '2', '', 'E10ADC3949BA59ABBE56E057F20F883E', '0', '0', '127.0.0.1', '2021-08-19 09:27:46', 'admin', '2021-08-18 18:24:53', '', NULL, NULL);


-- ----------------------------
-- Records of sys_user_role
-- ----------------------------
INSERT INTO `sys_user_role` VALUES (1, 1);

-- ----------------------------
-- Records of articleCategory
-- ----------------------------
INSERT INTO `articleCategory` VALUES (1, 'C#', '2021-08-13 00:00:00.000000', 0);
INSERT INTO `articleCategory` VALUES (2, 'java', '2021-08-18 00:00:00.000000', 0);
INSERT INTO `articleCategory` VALUES (3, '前端', '2021-08-18 00:00:00.000000', 0);
INSERT INTO `articleCategory` VALUES (4, '数据库', '2021-08-18 00:00:00.000000', 0);
INSERT INTO `articleCategory` VALUES (5, '其他', '2021-08-19 00:00:00.000000', 0);
INSERT INTO `articleCategory` VALUES (6, '羽毛球', '2021-08-19 00:00:00.000000', 5);
INSERT INTO `articleCategory` VALUES (7, 'vue', '2021-08-19 00:00:00.000000', 3);
INSERT INTO `articleCategory` VALUES (8, 'sqlserver', '2021-08-19 00:00:00.000000', 4);

INSERT INTO `sys_config`(`configId`, `configName`, `configKey`, `configValue`, `configType`, `create_by`, `create_time`, `update_by`, `update_time`, `remark`) 
VALUES (1, '主框架页-默认皮肤样式名称', 'sys.index.skinName', 'skin-blue', 'Y', 'admin', '2021-12-26 13:14:57', '', NULL, '蓝色 skin-blue、绿色 skin-green、紫色 skin-purple、红色 skin-red、黄色 skin-yellow');
INSERT INTO `sys_config`(`configId`, `configName`, `configKey`, `configValue`, `configType`, `create_by`, `create_time`, `update_by`, `update_time`, `remark`) 
VALUES (2, '用户管理-账号初始密码', 'sys.user.initPassword', '123456', 'Y', 'admin', '2021-12-26 13:14:57', '', NULL, '初始化密码 123456');
INSERT INTO `sys_config`(`configId`, `configName`, `configKey`, `configValue`, `configType`, `create_by`, `create_time`, `update_by`, `update_time`, `remark`) 
VALUES (3, '主框架页-侧边栏主题', 'sys.index.sideTheme', 'theme-dark', 'Y', 'admin', '2021-12-26 13:14:57', '', NULL, '深色主题theme-dark，浅色主题theme-light');
INSERT INTO `sys_config`(`configId`, `configName`, `configKey`, `configValue`, `configType`, `create_by`, `create_time`, `update_by`, `update_time`, `remark`) 
VALUES (4, '账号自助-验证码开关', 'sys.account.captchaOnOff', '1', 'Y', 'admin', '2021-12-26 13:14:57', 'admin', '2022-03-30 12:43:48', '是否开启验证码功能（off、关闭，1、动态验证码 2、动态gif泡泡 3、泡泡 4、静态验证码）');
INSERT INTO `sys_config`(`configId`, `configName`, `configKey`, `configValue`, `configType`, `create_by`, `create_time`, `update_by`, `update_time`, `remark`) 
VALUES (5, '本地文件上传访问域名', 'sys.file.uploadurl', 'http://localhost:8888', 'Y', '', sysdate(), '', NULL, NULL);
INSERT INTO `sys_config`(`configId`, `configName`, `configKey`, `configValue`, `configType`, `create_by`, `create_time`, `update_by`, `update_time`, `remark`) 
VALUES (6, '开启注册功能', 'sys.account.register', 'true', 'Y', 'admin', sysdate(), 'admin', NULL, NULL);
INSERT INTO `sys_config`(`configId`, `configName`, `configKey`, `configValue`, `configType`, `create_by`, `create_time`, `update_by`, `update_time`, `remark`) 
VALUES (7, '文章预览地址', 'sys.article.preview.url', 'http://www.izhaorui.cn/article/details/', 'Y', 'admin', sysdate(), '', NULL, '格式：http://www.izhaorui.cn/article/details/{aid}，其中{aid}为文章的id');
