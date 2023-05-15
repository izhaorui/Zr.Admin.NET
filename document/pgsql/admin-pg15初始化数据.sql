-- ----------------------------
-- Records of sys_user_role
-- ----------------------------
INSERT INTO "public"."sys_user_role" VALUES (1, 1);
INSERT INTO "public"."sys_user_role" VALUES (2, 2);



-- ----------------------------
-- Records of sys_config
-- ----------------------------
INSERT INTO "public"."sys_config" VALUES (1, '主框架页-默认皮肤样式名称', 'sys.index.skinName', 'skin-blue', 'Y', 'admin', '2021-12-26 13:14:57', '', NULL, '蓝色 skin-blue、绿色 skin-green、紫色 skin-purple、红色 skin-red、黄色 skin-yellow');
INSERT INTO "public"."sys_config" VALUES (2, '用户管理-账号初始密码', 'sys.user.initPassword', '123456', 'Y', 'admin', '2021-12-26 13:14:57', '', NULL, '初始化密码 123456');
INSERT INTO "public"."sys_config" VALUES (3, '主框架页-侧边栏主题', 'sys.index.sideTheme', 'theme-dark', 'Y', 'admin', '2021-12-26 13:14:57', '', NULL, '深色主题theme-dark，浅色主题theme-light');
INSERT INTO "public"."sys_config" VALUES (5, '本地文件上传访问域名', 'sys.file.uploadurl', 'http://localhost:8888', 'Y', '', '2022-12-19 10:12:37', '', NULL, NULL);
INSERT INTO "public"."sys_config" VALUES (6, '开启注册功能', 'sys.account.register', 'true', 'Y', 'admin', '2022-12-19 10:12:37', 'admin', NULL, NULL);
INSERT INTO "public"."sys_config" VALUES (7, '文章预览地址', 'sys.article.preview.url', 'http://www.izhaorui.cn/article/details/', 'Y', 'admin', '2022-12-19 10:12:37', '', NULL, '格式：http://www.izhaorui.cn/article/details/{aid}，其中{aid}为文章的id');
INSERT INTO "public"."sys_config" VALUES (4, '账号自助-验证码开关', 'sys.account.captchaOnOff', 'off', 'Y', 'admin', '2021-12-26 13:14:57', 'admin', '2022-03-30 12:43:48', '是否开启验证码功能（off、关闭，1、动态验证码 2、动态gif泡泡 3、泡泡 4、静态验证码）');



-- ----------------------------
-- Records of sys_dept
-- ----------------------------
INSERT INTO "public"."sys_dept" VALUES (100, 0, '0', 'A公司', 0, 'zr', '', '', '0', '0', 'admin', NULL, '', NULL, NULL);
INSERT INTO "public"."sys_dept" VALUES (101, 100, '0,100', '研发部门', 1, 'zr', '', '', '0', '0', 'admin', NULL, '', NULL, NULL);
INSERT INTO "public"."sys_dept" VALUES (102, 100, '0,100', '市场部门', 2, 'zr', '', '', '0', '0', 'admin', NULL, '', NULL, NULL);
INSERT INTO "public"."sys_dept" VALUES (103, 100, '0,100', '测试部门', 3, 'zr', '', '', '0', '0', 'admin', NULL, '', NULL, NULL);
INSERT INTO "public"."sys_dept" VALUES (104, 100, '0,100', '财务部门', 4, 'zr', '', '', '0', '0', 'admin', NULL, '', NULL, NULL);
INSERT INTO "public"."sys_dept" VALUES (200, 0, '0', 'B公司', 0, 'zr', '', '', '0', '0', 'admin', NULL, '', NULL, NULL);


-- ----------------------------
-- Records of sys_dict_data
-- ----------------------------
INSERT INTO "public"."sys_dict_data" VALUES (1, 1, '男', '0', 'sys_user_sex', '', '', 'Y', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '性别男');
INSERT INTO "public"."sys_dict_data" VALUES (2, 2, '女', '1', 'sys_user_sex', '', '', 'N', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '性别女');
INSERT INTO "public"."sys_dict_data" VALUES (3, 3, '未知', '2', 'sys_user_sex', '', '', 'N', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '性别未知');
INSERT INTO "public"."sys_dict_data" VALUES (4, 1, '显示', '0', 'sys_show_hide', '', 'primary', 'Y', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '显示菜单');
INSERT INTO "public"."sys_dict_data" VALUES (5, 2, '隐藏', '1', 'sys_show_hide', '', 'danger', 'N', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '隐藏菜单');
INSERT INTO "public"."sys_dict_data" VALUES (6, 1, '正常', '0', 'sys_normal_disable', '', 'primary', 'Y', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '正常状态');
INSERT INTO "public"."sys_dict_data" VALUES (7, 2, '停用', '1', 'sys_normal_disable', '', 'danger', 'N', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '停用状态');
INSERT INTO "public"."sys_dict_data" VALUES (8, 1, '正常', '0', 'sys_job_status', '', 'primary', 'Y', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '正常状态');
INSERT INTO "public"."sys_dict_data" VALUES (9, 2, '异常', '1', 'sys_job_status', '', 'danger', 'N', '0', 'admin', '2021-02-24 10:56:21', '', '2021-07-02 14:09:09', '停用状态');
INSERT INTO "public"."sys_dict_data" VALUES (10, 1, '默认', 'DEFAULT', 'sys_job_group', '', '', 'Y', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '默认分组');
INSERT INTO "public"."sys_dict_data" VALUES (11, 2, '系统', 'SYSTEM', 'sys_job_group', '', '', 'N', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '系统分组');
INSERT INTO "public"."sys_dict_data" VALUES (12, 1, '是', 'Y', 'sys_yes_no', '', 'primary', 'Y', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '系统默认是');
INSERT INTO "public"."sys_dict_data" VALUES (13, 2, '否', 'N', 'sys_yes_no', '', 'danger', 'N', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '系统默认否');
INSERT INTO "public"."sys_dict_data" VALUES (14, 1, '通知', '1', 'sys_notice_type', '', 'warning', 'Y', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '通知');
INSERT INTO "public"."sys_dict_data" VALUES (15, 2, '公告', '2', 'sys_notice_type', '', 'success', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '公告');
INSERT INTO "public"."sys_dict_data" VALUES (16, 1, '正常', '0', 'sys_notice_status', '', 'primary', 'Y', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '正常状态');
INSERT INTO "public"."sys_dict_data" VALUES (17, 2, '关闭', '1', 'sys_notice_status', '', 'danger', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '关闭状态');
INSERT INTO "public"."sys_dict_data" VALUES (18, 0, '其他', '0', 'sys_oper_type', '', 'info', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '其他操作');
INSERT INTO "public"."sys_dict_data" VALUES (19, 1, '新增', '1', 'sys_oper_type', '', 'info', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '新增操作');
INSERT INTO "public"."sys_dict_data" VALUES (20, 2, '修改', '2', 'sys_oper_type', '', 'info', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '修改操作');
INSERT INTO "public"."sys_dict_data" VALUES (21, 3, '删除', '3', 'sys_oper_type', '', 'danger', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '删除操作');
INSERT INTO "public"."sys_dict_data" VALUES (22, 4, '授权', '4', 'sys_oper_type', '', 'primary', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '授权操作');
INSERT INTO "public"."sys_dict_data" VALUES (23, 5, '导出', '5', 'sys_oper_type', '', 'warning', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '导出操作');
INSERT INTO "public"."sys_dict_data" VALUES (24, 6, '导入', '6', 'sys_oper_type', '', 'warning', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '导入操作');
INSERT INTO "public"."sys_dict_data" VALUES (25, 7, '强退', '7', 'sys_oper_type', '', 'danger', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '强退操作');
INSERT INTO "public"."sys_dict_data" VALUES (26, 8, '生成代码', '8', 'sys_oper_type', '', 'warning', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '生成操作');
INSERT INTO "public"."sys_dict_data" VALUES (27, 9, '清空数据', '9', 'sys_oper_type', '', 'danger', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '清空操作');
INSERT INTO "public"."sys_dict_data" VALUES (28, 1, '成功', '0', 'sys_common_status', '', 'primary', 'N', '0', 'admin', '2021-02-24 10:56:23', '', NULL, '正常状态');
INSERT INTO "public"."sys_dict_data" VALUES (29, 2, '失败', '1', 'sys_common_status', '', 'danger', 'N', '0', 'admin', '2021-02-24 10:56:23', '', NULL, '停用状态');
INSERT INTO "public"."sys_dict_data" VALUES (30, 1, '发布', '1', 'sys_article_status', NULL, NULL, NULL, '0', 'admin', '2021-08-19 10:34:56', '', NULL, NULL);
INSERT INTO "public"."sys_dict_data" VALUES (31, 2, '草稿', '2', 'sys_article_status', NULL, NULL, NULL, '0', 'admin', '2021-08-19 10:35:06', '', NULL, NULL);
INSERT INTO "public"."sys_dict_data" VALUES (32, 1, '中文', 'zh-cn', 'sys_lang_type', NULL, NULL, NULL, '0', 'admin', '2021-08-19 10:35:06', '', NULL, NULL);
INSERT INTO "public"."sys_dict_data" VALUES (33, 2, '英文', 'en', 'sys_lang_type', NULL, NULL, NULL, '0', 'admin', '2021-08-19 10:35:06', '', NULL, NULL);
INSERT INTO "public"."sys_dict_data" VALUES (34, 3, '繁体', 'zh-tw', 'sys_lang_type', NULL, NULL, NULL, '0', 'admin', '2021-08-19 10:35:06', '', NULL, NULL);


-- ----------------------------
-- Records of sys_dict_type
-- ----------------------------
INSERT INTO "public"."sys_dict_type" VALUES (1, '用户性别', 'sys_user_sex', '0', 'Y', 'admin', '2022-12-19 10:12:34', '', NULL, '用户性别列表', NULL);
INSERT INTO "public"."sys_dict_type" VALUES (2, '菜单状态', 'sys_show_hide', '0', 'Y', 'admin', '2022-12-19 10:12:34', '', NULL, '菜单状态列表', NULL);
INSERT INTO "public"."sys_dict_type" VALUES (3, '系统开关', 'sys_normal_disable', '0', 'Y', 'admin', '2022-12-19 10:12:34', '', NULL, '系统开关列表', NULL);
INSERT INTO "public"."sys_dict_type" VALUES (4, '任务状态', 'sys_job_status', '0', 'Y', 'admin', '2022-12-19 10:12:34', '', NULL, '任务状态列表', NULL);
INSERT INTO "public"."sys_dict_type" VALUES (5, '任务分组', 'sys_job_group', '0', 'Y', 'admin', '2022-12-19 10:12:34', '', NULL, '任务分组列表', NULL);
INSERT INTO "public"."sys_dict_type" VALUES (6, '系统是否', 'sys_yes_no', '0', 'Y', 'admin', '2022-12-19 10:12:34', '', NULL, '系统是否列表', NULL);
INSERT INTO "public"."sys_dict_type" VALUES (7, '通知类型', 'sys_notice_type', '0', 'Y', 'admin', '2022-12-19 10:12:34', '', NULL, '通知类型列表', NULL);
INSERT INTO "public"."sys_dict_type" VALUES (8, '通知状态', 'sys_notice_status', '0', 'Y', 'admin', '2022-12-19 10:12:34', '', NULL, '通知状态列表', NULL);
INSERT INTO "public"."sys_dict_type" VALUES (9, '操作类型', 'sys_oper_type', '0', 'Y', 'admin', '2022-12-19 10:12:34', '', NULL, '操作类型列表', NULL);
INSERT INTO "public"."sys_dict_type" VALUES (10, '系统状态', 'sys_common_status', '0', 'Y', 'admin', '2022-12-19 10:12:34', '', NULL, '登录状态列表', NULL);
INSERT INTO "public"."sys_dict_type" VALUES (11, '文章状态', 'sys_article_status', '0', 'Y', 'admin', '2022-12-19 10:12:34', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_dict_type" VALUES (12, '多语言类型', 'sys_lang_type', '0', 'Y', 'admin', '2022-12-19 10:12:34', '', NULL, '多语言字典类型', NULL);




-- ----------------------------
-- Records of sys_menu
-- ----------------------------
INSERT INTO "public"."sys_menu" VALUES (1, '系统管理', 0, 1, 'system', NULL, 0, 0, 'M', '0', '0', '', 'system', '', '2022-12-19 10:12:35', '', NULL, '系统管理目录', 'menu.system');
INSERT INTO "public"."sys_menu" VALUES (2, '系统监控', 0, 2, 'monitor', NULL, 0, 0, 'M', '0', '0', '', 'monitor', '', '2022-12-19 10:12:35', '', NULL, '系统监控目录', 'menu.monitoring');
INSERT INTO "public"."sys_menu" VALUES (3, '系统工具', 0, 3, 'tool', NULL, 0, 0, 'M', '0', '0', '', 'tool', '', '2022-12-19 10:12:35', '', NULL, '系统工具目录', 'menu.systemTools');
INSERT INTO "public"."sys_menu" VALUES (6, '控制台', 0, 0, 'dashboard', 'index_v1', 0, 0, 'C', '0', '0', '', 'dashboard', '', '2022-12-19 10:12:35', '', NULL, '', 'menu.dashboard');
INSERT INTO "public"."sys_menu" VALUES (100, '用户管理', 1, 1, 'user', 'system/user/index', 0, 0, 'C', '0', '0', 'system:user:list', 'user', '', '2022-12-19 10:12:35', '', NULL, '用户管理菜单', 'menu.systemUser');
INSERT INTO "public"."sys_menu" VALUES (101, '角色管理', 1, 2, 'role', 'system/role/index', 0, 0, 'C', '0', '0', 'system:role:list', 'peoples', '', '2022-12-19 10:12:35', '', NULL, '角色管理菜单', 'menu.systemRole');
INSERT INTO "public"."sys_menu" VALUES (102, '菜单管理', 1, 3, 'menu', 'system/menu/index', 0, 0, 'C', '0', '0', 'system:menu:list', 'tree-table', '', '2022-12-19 10:12:35', '', NULL, '菜单管理菜单', 'menu.systemMenu');
INSERT INTO "public"."sys_menu" VALUES (103, '部门管理', 1, 4, 'dept', 'system/dept/index', 0, 0, 'C', '0', '0', 'system:dept:list', 'tree', '', '2022-12-19 10:12:35', '', NULL, '部门管理菜单', 'menu.systemDept');
INSERT INTO "public"."sys_menu" VALUES (104, '岗位管理', 1, 5, 'post', 'system/post/index', 0, 0, 'C', '0', '0', 'system:post:list', 'post', '', '2022-12-19 10:12:35', '', NULL, '岗位管理菜单', 'menu.systemPost');
INSERT INTO "public"."sys_menu" VALUES (105, '字典管理', 1, 6, 'dict', 'system/dict/index', 0, 0, 'C', '0', '0', 'system:dict:list', 'dict', '', '2022-12-19 10:12:35', '', NULL, '', 'menu.systemDic');
INSERT INTO "public"."sys_menu" VALUES (106, '角色分配', 1, 2, 'roleusers', 'system/roleusers/index', 0, 0, 'C', '1', '0', 'system:roleusers:list', 'people', '', '2022-12-19 10:12:35', '', NULL, NULL, '');
INSERT INTO "public"."sys_menu" VALUES (107, '参数设置', 1, 8, 'config', 'system/config/index', 0, 0, 'C', '0', '0', 'system:config:list', 'edit', '', '2022-12-19 10:12:35', '', NULL, '', 'menu.systemParam');
INSERT INTO "public"."sys_menu" VALUES (108, '日志管理', 1, 10, 'log', '', 0, 0, 'M', '0', '0', '', 'log', '', '2022-12-19 10:12:35', '', NULL, '日志管理菜单', 'menu.systemLog');
INSERT INTO "public"."sys_menu" VALUES (109, '通知公告', 1, 9, 'notice', 'system/notice/index', 0, 0, 'C', '0', '0', 'system:notice:list', 'message', '', '2022-12-19 10:12:35', '', NULL, '通知公告菜单', 'menu.systemNotice');
INSERT INTO "public"."sys_menu" VALUES (110, '定时任务', 2, 10, 'job', 'monitor/job/index', 0, 0, 'C', '0', '0', '', 'job', '', '2022-12-19 10:12:35', '', NULL, '定时任务菜单', 'menu.timedTask');
INSERT INTO "public"."sys_menu" VALUES (112, '服务监控', 2, 11, 'server', 'monitor/server/index', 0, 0, 'C', '0', '0', 'monitor:server:list', 'server', '', '2022-12-19 10:12:35', '', NULL, '服务监控菜单', 'menu.serviceMonitor');
INSERT INTO "public"."sys_menu" VALUES (113, '缓存监控', 2, 12, 'cache', 'monitor/cache/index', 0, 0, 'C', '1', '1', 'monitor:cache:list', 'redis', '', '2022-12-19 10:12:35', '', NULL, '缓存监控菜单', 'menu.cacheMonitor');
INSERT INTO "public"."sys_menu" VALUES (114, '表单构建', 3, 13, 'build', 'tool/build/index', 0, 0, 'C', '0', '0', 'tool:build:list', 'build', '', '2022-12-19 10:12:35', '', NULL, '表单构建菜单', 'menu.formBuild');
INSERT INTO "public"."sys_menu" VALUES (115, '代码生成', 3, 14, 'gen', 'tool/gen/index', 0, 0, 'C', '0', '0', 'tool:gen:list', 'code', '', '2022-12-19 10:12:35', '', NULL, '代码生成菜单', 'menu.codeGeneration');
INSERT INTO "public"."sys_menu" VALUES (116, '系统接口', 3, 15, 'swagger', 'tool/swagger/index', 0, 0, 'C', '0', '0', 'tool:swagger:list', 'swagger', '', '2022-12-19 10:12:35', '', NULL, '系统接口菜单', 'menu.systemInterface');
INSERT INTO "public"."sys_menu" VALUES (117, '发送邮件', 3, 16, 'sendEmail', 'tool/email/sendEmail', 0, 0, 'C', '0', '0', 'tool:email:send', 'email', '', '2022-12-19 10:12:35', '', NULL, '发送邮件菜单', 'menu.sendEmail');
INSERT INTO "public"."sys_menu" VALUES (118, '文章管理', 3, 18, 'article', NULL, 0, 0, 'M', '0', '0', NULL, 'documentation', '', '2022-12-19 10:12:35', '', NULL, NULL, 'menu.systemArticle');
INSERT INTO "public"."sys_menu" VALUES (119, '文章列表', 118, 1, 'index', 'system/article/manager', 0, 0, 'C', '0', '0', 'system:article:list', 'list', '', '2022-12-19 10:12:35', '', NULL, NULL, 'menu.articleList');
INSERT INTO "public"."sys_menu" VALUES (500, '操作日志', 108, 1, 'operlog', 'monitor/operlog/index', 0, 0, 'C', '0', '0', 'monitor:operlog:list', 'form', '', '2022-12-19 10:12:35', '', NULL, '操作日志菜单', 'menu.operLog');
INSERT INTO "public"."sys_menu" VALUES (501, '登录日志', 108, 2, 'logininfor', 'monitor/logininfor/index', 0, 0, 'C', '0', '0', 'monitor:logininfor:list', 'logininfor', '', '2022-12-19 10:12:35', '', NULL, '登录日志菜单', 'menu.loginLog');
INSERT INTO "public"."sys_menu" VALUES (1001, '用户查询', 100, 1, '', '', 0, 0, 'F', '0', '0', 'system:user:query', '', '', '2022-12-19 10:12:35', '', NULL, '', NULL);
INSERT INTO "public"."sys_menu" VALUES (1002, '用户添加', 100, 2, '', '', 0, 0, 'F', '0', '0', 'system:user:add', '', '', '2022-12-19 10:12:35', '', NULL, '', NULL);
INSERT INTO "public"."sys_menu" VALUES (1003, '用户修改', 100, 3, '', '', 0, 0, 'F', '0', '0', 'system:user:edit', '', '', '2022-12-19 10:12:35', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1004, '用户删除', 100, 4, '', '', 0, 0, 'F', '0', '0', 'system:user:delete', '', '', '2022-12-19 10:12:35', '', NULL, '', NULL);
INSERT INTO "public"."sys_menu" VALUES (1005, '用户导出', 100, 5, '', '', 0, 0, 'F', '0', '0', 'system:user:export', '#', '', '2022-12-19 10:12:35', '', NULL, '', NULL);
INSERT INTO "public"."sys_menu" VALUES (1006, '用户导入', 100, 6, '', '', 0, 0, 'F', '0', '0', 'system:user:import', '#', '', '2022-12-19 10:12:35', '', NULL, '', NULL);
INSERT INTO "public"."sys_menu" VALUES (1007, '重置密码', 100, 7, '', '', 0, 0, 'F', '0', '0', 'system:user:resetPwd', '#', '', '2022-12-19 10:12:35', '', NULL, '', NULL);
INSERT INTO "public"."sys_menu" VALUES (1008, '角色查询', 101, 1, '', '', 0, 0, 'F', '0', '0', 'system:role:query', '#', '', '2022-12-19 10:12:35', '', NULL, '', NULL);
INSERT INTO "public"."sys_menu" VALUES (1009, '角色新增', 101, 2, '', '', 0, 0, 'F', '0', '0', 'system:role:add', '#', '', '2022-12-19 10:12:35', '', NULL, '', NULL);
INSERT INTO "public"."sys_menu" VALUES (1010, '角色修改', 101, 3, '', '', 0, 0, 'F', '0', '0', 'system:role:edit', '#', '', '2022-12-19 10:12:35', '', NULL, '', NULL);
INSERT INTO "public"."sys_menu" VALUES (1011, '角色删除', 101, 4, '', '', 0, 0, 'F', '0', '0', 'system:role:remove', '#', '', '2022-12-19 10:12:35', '', NULL, '', NULL);
INSERT INTO "public"."sys_menu" VALUES (1012, '角色授权', 101, 5, '', '', 0, 0, 'F', '0', '0', 'system:role:authorize', '#', '', '2022-12-19 10:12:35', '', NULL, '', NULL);
INSERT INTO "public"."sys_menu" VALUES (1013, '菜单查询', 102, 1, '', '', 0, 0, 'F', '0', '0', 'system:menu:query', '#', '', '2022-12-19 10:12:35', '', NULL, '', NULL);
INSERT INTO "public"."sys_menu" VALUES (1014, '菜单新增', 102, 2, '', '', 0, 0, 'F', '0', '0', 'system:menu:add', '#', '', '2022-12-19 10:12:35', '', NULL, '', NULL);
INSERT INTO "public"."sys_menu" VALUES (1015, '菜单修改', 102, 3, '', '', 0, 0, 'F', '0', '0', 'system:menu:edit', '#', '', '2022-12-19 10:12:35', '', NULL, '', NULL);
INSERT INTO "public"."sys_menu" VALUES (1016, '菜单删除', 102, 4, '', '', 0, 0, 'F', '0', '0', 'system:menu:remove', '#', '', '2022-12-19 10:12:35', '', NULL, '', NULL);
INSERT INTO "public"."sys_menu" VALUES (1017, '修改排序', 102, 5, '', '', 0, 0, 'F', '0', '0', 'system:menu:changeSort', '', '', '2022-12-19 10:12:35', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1018, '部门查询', 103, 1, '', '', 0, 0, 'F', '0', '0', 'system:dept:query', '', '', '2022-12-19 10:12:35', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1019, '部门新增', 103, 2, '', '', 0, 0, 'F', '0', '0', 'system:dept:add', NULL, '', '2022-12-19 10:12:35', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1020, '部门修改', 103, 3, '', '', 0, 0, 'F', '0', '0', 'system:dept:update', NULL, '', '2022-12-19 10:12:35', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1021, '部门删除', 103, 4, '', '', 0, 0, 'F', '0', '0', 'system:dept:remove', NULL, '', '2022-12-19 10:12:35', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1022, '岗位查询', 104, 1, '', '', 0, 0, 'F', '0', '0', 'system:post:list', NULL, '', '2022-12-19 10:12:35', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1023, '岗位添加', 104, 2, '', '', 0, 0, 'F', '0', '0', 'system:post:add', NULL, '', '2022-12-19 10:12:35', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1024, '岗位删除', 104, 3, '', '', 0, 0, 'F', '0', '0', 'system:post:remove', NULL, '', '2022-12-19 10:12:35', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1025, '岗位编辑', 104, 4, '', '', 0, 0, 'F', '0', '0', 'system:post:edit', '', '', '2022-12-19 10:12:35', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1026, '字典新增', 105, 1, '', '', 0, 0, 'F', '0', '0', 'system:dict:add', NULL, '', '2022-12-19 10:12:35', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1027, '字典修改', 105, 2, '', '', 0, 0, 'F', '0', '0', 'system:dict:edit', NULL, '', '2022-12-19 10:12:35', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1028, '字典删除', 105, 3, '', '', 0, 0, 'F', '0', '0', 'system:dict:remove', NULL, '', '2022-12-19 10:12:35', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1029, '新增用户', 106, 2, '', '', 0, 0, 'F', '0', '0', 'system:roleusers:add', NULL, '', '2022-12-19 10:12:35', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1030, '删除用户', 106, 3, '', '', 0, 0, 'F', '0', '0', 'system:roleusers:remove', NULL, '', '2022-12-19 10:12:35', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1032, '任务查询', 110, 1, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:job:list', '#', '', '2022-12-19 10:12:35', '', NULL, '', NULL);
INSERT INTO "public"."sys_menu" VALUES (1033, '任务新增', 110, 2, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:job:add', '', '', '2022-12-19 10:12:35', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1034, '任务删除', 110, 3, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:job:delete', '', '', '2022-12-19 10:12:35', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1035, '任务修改', 110, 4, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:job:edit', '', '', '2022-12-19 10:12:35', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1036, '任务启动', 110, 5, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:job:start', '', '', '2022-12-19 10:12:35', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1037, '任务运行', 110, 7, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:job:run', NULL, '', '2022-12-19 10:12:35', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1038, '任务停止', 110, 8, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:job:stop', NULL, '', '2022-12-19 10:12:35', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1039, '任务日志', 2, 0, 'job/log', 'monitor/job/log', 0, 0, 'C', '1', '0', 'monitor:job:query', 'log', '', '2022-12-19 10:12:35', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1040, '任务导出', 110, 10, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:job:export', NULL, '', '2022-12-19 10:12:36', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1041, '操作查询', 500, 1, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:operlog:query', '', '', '2022-12-19 10:12:36', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1042, '操作删除', 500, 2, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:operlog:remove', '', '', '2022-12-19 10:12:36', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1043, '操作日志导出', 500, 3, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:operlog:export', '', '', '2022-12-19 10:12:36', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1044, '登录查询', 501, 1, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:logininfor:query', '', '', '2022-12-19 10:12:36', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1045, '登录删除', 501, 1, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:logininfor:remove', '', '', '2022-12-19 10:12:36', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1046, '登录日志导出', 501, 1, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:logininfor:export', '', '', '2022-12-19 10:12:36', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1047, '发布文章', 3, 2, '/article/publish', 'system/article/publish', 0, 0, 'C', '1', '0', 'system:article:publish', 'log', '', '2022-12-19 10:12:35', '', NULL, NULL, '');
INSERT INTO "public"."sys_menu" VALUES (1048, '文章新增', 118, 3, '#', NULL, 0, 0, 'F', '0', '0', 'system:article:add', '', '', '2022-12-19 10:12:36', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1049, '文章修改', 118, 4, '#', NULL, 0, 0, 'F', '0', '0', 'system:article:update', '', '', '2022-12-19 10:12:36', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1050, '文章删除', 118, 5, '#', NULL, 0, 0, 'F', '0', '0', 'system:article:delete', '', '', '2022-12-19 10:12:36', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1051, '查询公告', 109, 1, '#', NULL, 0, 0, 'F', '0', '0', 'system:notice:query', '', '', '2022-12-19 10:12:36', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1052, '新增公告', 109, 2, '#', NULL, 0, 0, 'F', '0', '0', 'system:notice:add', '', '', '2022-12-19 10:12:36', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1053, '删除公告', 109, 3, '#', NULL, 0, 0, 'F', '0', '0', 'system:notice:delete', '', '', '2022-12-19 10:12:36', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1054, '修改公告', 109, 4, '#', NULL, 0, 0, 'F', '0', '0', 'system:notice:update', '', '', '2022-12-19 10:12:36', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1055, '导出公告', 109, 5, '#', NULL, 0, 0, 'F', '0', '0', 'system:notice:export', '', '', '2022-12-19 10:12:36', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1060, '生成修改', 3, 1, '/gen/editTable', 'tool/gen/editTable', 0, 0, 'C', '1', '0', 'tool:gen:edit', '', '', '2022-12-19 10:12:36', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1061, '生成查询', 115, 1, '#', NULL, 0, 0, 'F', '0', '0', 'tool:gen:query', '', '', '2022-12-19 10:12:36', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1062, '生成删除', 115, 1, '#', NULL, 0, 0, 'F', '0', '0', 'tool:gen:remove', '', '', '2022-12-19 10:12:36', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1063, '导入代码', 115, 1, '#', NULL, 0, 0, 'F', '0', '0', 'tool:gen:import', '', '', '2022-12-19 10:12:36', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1064, '生成代码', 115, 1, '#', NULL, 0, 0, 'F', '0', '0', 'tool:gen:code', '', '', '2022-12-19 10:12:36', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1065, '预览代码', 115, 1, '#', NULL, 0, 0, 'F', '0', '0', 'tool:gen:preview', '', '', '2022-12-19 10:12:36', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1070, '岗位导出', 104, 4, '', '', 0, 0, 'F', '0', '0', 'system:post:export', '', '', '2022-12-19 10:12:35', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (1071, '字典导出', 105, 3, '', '', 0, 0, 'F', '0', '0', 'system:dict:export', NULL, '', '2022-12-19 10:12:35', '', NULL, NULL, NULL);
INSERT INTO "public"."sys_menu" VALUES (2000, '文件存储', 3, 17, 'file', 'tool/file/index', 0, 0, 'C', '0', '0', 'tool:file:list', 'upload', '', '2022-12-19 10:12:36', '', NULL, '文件存储菜单', 'menu.fileStorage');
INSERT INTO "public"."sys_menu" VALUES (2001, '查询', 2000, 1, '#', NULL, 0, 0, 'F', '0', '0', 'tool:file:query', '', '', '2022-12-19 10:12:36', '', NULL, '', NULL);
INSERT INTO "public"."sys_menu" VALUES (2002, '新增', 2000, 2, '#', NULL, 0, 0, 'F', '0', '0', 'tool:file:add', '', '', '2022-12-19 10:12:36', '', NULL, '', NULL);
INSERT INTO "public"."sys_menu" VALUES (2003, '删除', 2000, 3, '#', NULL, 0, 0, 'F', '0', '0', 'tool:file:delete', '', '', '2022-12-19 10:12:36', '', NULL, '', NULL);
INSERT INTO "public"."sys_menu" VALUES (2004, '修改', 2000, 4, '#', NULL, 0, 0, 'F', '0', '0', 'tool:file:update', '', '', '2022-12-19 10:12:36', '', NULL, '', NULL);
INSERT INTO "public"."sys_menu" VALUES (2005, '导出', 2000, 5, '#', NULL, 0, 0, 'F', '0', '0', 'tool:file:export', '', '', '2022-12-19 10:12:36', '', NULL, '', NULL);
INSERT INTO "public"."sys_menu" VALUES (2006, '多语言配置', 1, 20, 'CommonLang', 'system/commonLang/index', 0, 0, 'C', '0', '0', 'system:lang:list', 'language', 'system', '2022-12-19 10:12:36', '', NULL, '', 'menu.systemLang');
INSERT INTO "public"."sys_menu" VALUES (2007, '查询', 2006, 1, '#', NULL, 0, 0, 'F', '0', '0', 'system:lang:query', '', 'system', '2022-12-19 10:12:36', '', NULL, '', NULL);
INSERT INTO "public"."sys_menu" VALUES (2008, '新增', 2006, 2, '#', NULL, 0, 0, 'F', '0', '0', 'system:lang:add', '', 'system', '2022-12-19 10:12:36', '', NULL, '', NULL);
INSERT INTO "public"."sys_menu" VALUES (2009, '删除', 2006, 3, '#', NULL, 0, 0, 'F', '0', '0', 'system:lang:delete', '', 'system', '2022-12-19 10:12:36', '', NULL, '', NULL);
INSERT INTO "public"."sys_menu" VALUES (2010, '修改', 2006, 4, '#', NULL, 0, 0, 'F', '0', '0', 'system:lang:edit', '', 'system', '2022-12-19 10:12:36', '', NULL, '', NULL);
INSERT INTO "public"."sys_menu" VALUES (2011, '文章目录', 118, 999, 'ArticleCategory', 'system/article/articleCategory', 0, 0, 'C', '0', '0', 'articlecategory:list', 'tree-table', 'system', '2022-12-19 10:12:36', '', NULL, '', NULL);
INSERT INTO "public"."sys_menu" VALUES (2012, '查询', 2011, 1, '#', NULL, 0, 0, 'F', '0', '0', 'articlecategory:query', '', 'system', '2022-12-19 10:12:36', '', NULL, '', NULL);
INSERT INTO "public"."sys_menu" VALUES (2013, '新增', 2011, 2, '#', NULL, 0, 0, 'F', '0', '0', 'articlecategory:add', '', 'system', '2022-12-19 10:12:36', '', NULL, '', NULL);
INSERT INTO "public"."sys_menu" VALUES (2014, '删除', 2011, 3, '#', NULL, 0, 0, 'F', '0', '0', 'articlecategory:delete', '', 'system', '2022-12-19 10:12:36', '', NULL, '', NULL);
INSERT INTO "public"."sys_menu" VALUES (2015, '修改', 2011, 4, '#', NULL, 0, 0, 'F', '0', '0', 'articlecategory:edit', '', 'system', '2022-12-19 10:12:36', '', NULL, '', NULL);



-- ----------------------------
-- Records of sys_post
-- ----------------------------
INSERT INTO "public"."sys_post" VALUES (1, 'CEO', '董事长', 1, '0', '', '2022-12-19 10:12:36', '', NULL, '');
INSERT INTO "public"."sys_post" VALUES (2, 'SE', '项目经理', 2, '0', '', '2022-12-19 10:12:36', '', NULL, '');
INSERT INTO "public"."sys_post" VALUES (3, 'HR', '人力资源', 3, '0', '', '2022-12-19 10:12:36', '', NULL, '');
INSERT INTO "public"."sys_post" VALUES (4, 'USER', '普通员工', 4, '0', '', '2022-12-19 10:12:36', '', NULL, '');
INSERT INTO "public"."sys_post" VALUES (6, 'PM', '人事经理', 0, '0', NULL, '2022-12-19 10:12:36', '', NULL, NULL);
INSERT INTO "public"."sys_post" VALUES (7, 'GM', '总经理', 0, '0', NULL, '2022-12-19 10:12:36', '', NULL, NULL);
INSERT INTO "public"."sys_post" VALUES (8, 'COO', '首席运营官', 0, '0', NULL, '2022-12-19 10:12:36', '', NULL, NULL);
INSERT INTO "public"."sys_post" VALUES (9, 'CFO', '首席财务官', 0, '0', NULL, '2022-12-19 10:12:36', '', NULL, NULL);
INSERT INTO "public"."sys_post" VALUES (10, 'CTO', '首席技术官', 0, '0', NULL, '2022-12-19 10:12:36', '', NULL, NULL);
INSERT INTO "public"."sys_post" VALUES (11, 'HRD', '人力资源总监', 0, '0', NULL, '2022-12-19 10:12:36', '', NULL, NULL);
INSERT INTO "public"."sys_post" VALUES (12, 'VP', '副总裁', 0, '0', NULL, '2022-12-19 10:12:36', '', NULL, NULL);
INSERT INTO "public"."sys_post" VALUES (13, 'OD', '运营总监', 0, '0', NULL, '2022-12-19 10:12:36', '', NULL, NULL);
INSERT INTO "public"."sys_post" VALUES (14, 'MD', '市场总监', 0, '0', NULL, '2022-12-19 10:12:36', '', NULL, NULL);



-- ----------------------------
-- Records of sys_role
-- ----------------------------
INSERT INTO "public"."sys_role" VALUES (1, '超级管理员', 'admin', 1, '1', 1, 0, '0', '0', 'admin', '2022-12-19 10:12:36', 'system', NULL, '超级管理员');
INSERT INTO "public"."sys_role" VALUES (2, '普通角色', 'common', 2, '2', 1, 0, '0', '0', 'admin', '2022-12-19 10:12:36', 'system', NULL, '普通角色');


-- ----------------------------
-- Records of sys_role_dept
-- ----------------------------
INSERT INTO "public"."sys_role_dept" VALUES (2, 100);
INSERT INTO "public"."sys_role_dept" VALUES (2, 101);
INSERT INTO "public"."sys_role_dept" VALUES (2, 105);



-- ----------------------------
-- Records of sys_role_menu
-- ----------------------------
INSERT INTO "public"."sys_role_menu" VALUES (2, 1, NULL, '2022-12-19 10:12:36');
INSERT INTO "public"."sys_role_menu" VALUES (2, 3, NULL, '2022-12-19 10:12:36');
INSERT INTO "public"."sys_role_menu" VALUES (2, 5, NULL, '2022-12-19 10:12:36');
INSERT INTO "public"."sys_role_menu" VALUES (2, 6, NULL, '2022-12-19 10:12:36');
INSERT INTO "public"."sys_role_menu" VALUES (2, 100, 'admin', '2022-12-19 10:12:37');
INSERT INTO "public"."sys_role_menu" VALUES (2, 101, 'admin', '2022-12-19 10:12:37');
INSERT INTO "public"."sys_role_menu" VALUES (2, 102, 'admin', '2022-12-19 10:12:37');
INSERT INTO "public"."sys_role_menu" VALUES (2, 103, 'admin', '2022-12-19 10:12:37');
INSERT INTO "public"."sys_role_menu" VALUES (2, 104, 'admin', '2022-12-19 10:12:37');
INSERT INTO "public"."sys_role_menu" VALUES (2, 106, 'admin', '2022-12-19 10:12:37');
INSERT INTO "public"."sys_role_menu" VALUES (2, 108, 'admin', '2022-12-19 10:12:37');
INSERT INTO "public"."sys_role_menu" VALUES (2, 109, 'admin', '2022-12-19 10:12:37');
INSERT INTO "public"."sys_role_menu" VALUES (2, 114, 'admin', '2022-12-19 10:12:37');
INSERT INTO "public"."sys_role_menu" VALUES (2, 500, 'admin', '2022-12-19 10:12:37');
INSERT INTO "public"."sys_role_menu" VALUES (2, 501, 'admin', '2022-12-19 10:12:37');
INSERT INTO "public"."sys_role_menu" VALUES (2, 1001, 'admin', '2022-12-19 10:12:37');
INSERT INTO "public"."sys_role_menu" VALUES (2, 1008, 'admin', '2022-12-19 10:12:37');
INSERT INTO "public"."sys_role_menu" VALUES (2, 1013, 'admin', '2022-12-19 10:12:37');
INSERT INTO "public"."sys_role_menu" VALUES (2, 1018, 'admin', '2022-12-19 10:12:37');
INSERT INTO "public"."sys_role_menu" VALUES (2, 1022, 'admin', '2022-12-19 10:12:37');
INSERT INTO "public"."sys_role_menu" VALUES (2, 1031, 'admin', '2022-12-19 10:12:37');
INSERT INTO "public"."sys_role_menu" VALUES (2, 1044, 'admin', '2022-12-19 10:12:37');
INSERT INTO "public"."sys_role_menu" VALUES (2, 1051, 'admin', '2022-12-19 10:12:37');


-- ----------------------------
-- Records of sys_user
-- ----------------------------
INSERT INTO "public"."sys_user" VALUES (2, 0, 'user', 'user', '0', NULL, NULL, '0', '', 'E10ADC3949BA59ABBE56E057F20F883E', '0', '0', '', '0001-01-01 00:00:00', 'admin', '2021-07-05 17:29:13', 'admin', '2021-08-02 16:53:04', '普通用户');
INSERT INTO "public"."sys_user" VALUES (1, 0, 'admin', '管理员', '0', '', '', '0', '', 'E10ADC3949BA59ABBE56E057F20F883E', '0', '0', '127.0.0.1', '2022-12-20 15:07:58.729401', 'admin', '2020-11-26 11:52:59', 'admin', '2021-08-03 10:11:24', '管理员');

