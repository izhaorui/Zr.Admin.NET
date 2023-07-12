--CREATE DATABASE ZrAdmin
GO
USE ZrAdmin
GO
INSERT INTO sys_tasks VALUES ('1410905433996136448', '测试任务', 'SYSTEM', '0 0/10 * * * ? ', 'ZR.Tasks', 'TaskScheduler.Job_SyncTest', NULL, 0, '2021-07-02 18:17:31', '9999-12-31 00:00:00', 1, 1, 1, NULL, '2021-07-02 18:17:23', '2021-07-02 18:17:31', 'admin', NULL, NULL, 1, '', '', '');
GO
INSERT INTO sys_dept(parentId, deptName, orderNum, status, delFlag, create_by, create_time) VALUES (0,   'XXX公司', 0,0, 0, 'admin', GETDATE() );
INSERT INTO sys_dept(parentId, deptName, orderNum, status, delFlag, create_by, create_time) VALUES (100, '研发部门',1,0, 0, 'admin', GETDATE());
INSERT INTO sys_dept(parentId, deptName, orderNum, status, delFlag, create_by, create_time) VALUES (100, '市场部门',2,0, 0, 'admin', GETDATE());
INSERT INTO sys_dept(parentId, deptName, orderNum, status, delFlag, create_by, create_time) VALUES (100, '测试部门',3,0, 0, 'admin', GETDATE());
INSERT INTO sys_dept(parentId, deptName, orderNum, status, delFlag, create_by, create_time) VALUES (100, '财务部门',4,0, 0, 'admin', GETDATE());
GO
INSERT INTO sys_dict_type VALUES ('用户性别', 'sys_user_sex', '0', 'Y', 'admin', '2021-02-24 10:55:26', '', NULL, '用户性别列表', NULL);
INSERT INTO sys_dict_type VALUES ('菜单状态', 'sys_show_hide', '0', 'Y', 'admin', '2021-02-24 10:55:26', '', NULL, '菜单状态列表', NULL);
INSERT INTO sys_dict_type VALUES ('系统开关', 'sys_normal_disable', '0', 'Y', 'admin', '2021-02-24 10:55:26', '', NULL, '系统开关列表', NULL);
INSERT INTO sys_dict_type VALUES ('任务状态', 'sys_job_status', '0', 'Y', 'admin', '2021-02-24 10:55:26', '', NULL, '任务状态列表', NULL);
INSERT INTO sys_dict_type VALUES ('任务分组', 'sys_job_group', '0', 'Y', 'admin', '2021-02-24 10:55:26', '', NULL, '任务分组列表', NULL);
INSERT INTO sys_dict_type VALUES ('系统是否', 'sys_yes_no', '0', 'Y', 'admin', '2021-02-24 10:55:26', '', NULL, '系统是否列表', NULL);
INSERT INTO sys_dict_type VALUES ('通知类型', 'sys_notice_type', 'Y', '0', 'admin', '2021-02-24 10:55:26', '', NULL, '通知类型列表', NULL);
INSERT INTO sys_dict_type VALUES ('通知状态', 'sys_notice_status', '0', 'Y', 'admin', '2021-02-24 10:55:26', '', NULL, '通知状态列表', NULL);
INSERT INTO sys_dict_type VALUES ('操作类型', 'sys_oper_type', '0', 'Y', 'admin', '2021-02-24 10:55:26', '', NULL, '操作类型列表', NULL);
INSERT INTO sys_dict_type VALUES ('系统状态', 'sys_common_status', '0', 'Y', 'admin', '2021-02-24 10:55:27', '', NULL, '登录状态列表', NULL);
INSERT INTO sys_dict_type VALUES ('文章状态', 'sys_article_status', '0', 'Y', 'admin', '2021-08-19 10:34:33', '', NULL, NULL, NULL);
INSERT INTO sys_dict_type VALUES ('多语言类型', 'sys_lang_type', '0', 'Y', 'admin', '2021-08-19 10:34:33', '', NULL, '多语言字典类型', NULL);

GO

INSERT INTO sys_dict_data VALUES (1, '男', '0', 'sys_user_sex', '', '', 'Y', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '性别男');
INSERT INTO sys_dict_data VALUES (2, '女', '1', 'sys_user_sex', '', '', 'N', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '性别女');
INSERT INTO sys_dict_data VALUES (3, '未知', '2', 'sys_user_sex', '', '', 'N', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '性别未知');
INSERT INTO sys_dict_data VALUES (1, '显示', '0', 'sys_show_hide', '', 'primary', 'Y', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '显示菜单');
INSERT INTO sys_dict_data VALUES (2, '隐藏', '1', 'sys_show_hide', '', 'danger', 'N', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '隐藏菜单');
INSERT INTO sys_dict_data VALUES (1, '正常', '0', 'sys_normal_disable', '', 'primary', 'Y', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '正常状态');
INSERT INTO sys_dict_data VALUES (2, '停用', '1', 'sys_normal_disable', '', 'danger', 'N', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '停用状态');
INSERT INTO sys_dict_data VALUES (1, '正常', '0', 'sys_job_status', '', 'primary', 'Y', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '正常状态');
INSERT INTO sys_dict_data VALUES (2, '异常', '1', 'sys_job_status', '', 'danger', 'N', '0', 'admin', '2021-02-24 10:56:21', '', '2021-07-02 14:09:09', '停用状态');
INSERT INTO sys_dict_data VALUES ( 1, '默认', 'DEFAULT', 'sys_job_group', '', '', 'Y', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '默认分组');
INSERT INTO sys_dict_data VALUES ( 2, '系统', 'SYSTEM', 'sys_job_group', '', '', 'N', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '系统分组');
INSERT INTO sys_dict_data VALUES ( 1, '是', 'Y', 'sys_yes_no', '', 'primary', 'Y', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '系统默认是');
INSERT INTO sys_dict_data VALUES ( 2, '否', 'N', 'sys_yes_no', '', 'danger', 'N', '0', 'admin', '2021-02-24 10:56:21', '', NULL, '系统默认否');
INSERT INTO sys_dict_data VALUES ( 1, '通知', '1', 'sys_notice_type', '', 'warning', 'Y', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '通知');
INSERT INTO sys_dict_data VALUES ( 2, '公告', '2', 'sys_notice_type', '', 'success', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '公告');
INSERT INTO sys_dict_data VALUES ( 1, '正常', '0', 'sys_notice_status', '', 'primary', 'Y', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '正常状态');
INSERT INTO sys_dict_data VALUES ( 2, '关闭', '1', 'sys_notice_status', '', 'danger', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '关闭状态');
INSERT INTO sys_dict_data VALUES ( 0, '其他', '0', 'sys_oper_type', '', 'info', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '其他操作');
INSERT INTO sys_dict_data VALUES ( 1, '新增', '1', 'sys_oper_type', '', 'info', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '新增操作');
INSERT INTO sys_dict_data VALUES ( 2, '修改', '2', 'sys_oper_type', '', 'info', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '修改操作');
INSERT INTO sys_dict_data VALUES ( 3, '删除', '3', 'sys_oper_type', '', 'danger', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '删除操作');
INSERT INTO sys_dict_data VALUES ( 4, '授权', '4', 'sys_oper_type', '', 'primary', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '授权操作');
INSERT INTO sys_dict_data VALUES ( 5, '导出', '5', 'sys_oper_type', '', 'warning', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '导出操作');
INSERT INTO sys_dict_data VALUES ( 6, '导入', '6', 'sys_oper_type', '', 'warning', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '导入操作');
INSERT INTO sys_dict_data VALUES ( 7, '强退', '7', 'sys_oper_type', '', 'danger', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '强退操作');
INSERT INTO sys_dict_data VALUES ( 8, '生成代码', '8', 'sys_oper_type', '', 'warning', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '生成操作');
INSERT INTO sys_dict_data VALUES ( 9, '清空数据', '9', 'sys_oper_type', '', 'danger', 'N', '0', 'admin', '2021-02-24 10:56:22', '', NULL, '清空操作');
INSERT INTO sys_dict_data VALUES ( 1, '成功', '0', 'sys_common_status', '', 'primary', 'N', '0', 'admin', '2021-02-24 10:56:23', '', NULL, '正常状态');
INSERT INTO sys_dict_data VALUES ( 2, '失败', '1', 'sys_common_status', '', 'danger', 'N', '0', 'admin', '2021-02-24 10:56:23', '', NULL, '停用状态');
INSERT INTO sys_dict_data VALUES ( 1, '发布', '1', 'sys_article_status', NULL, NULL, NULL, '0', 'admin', '2021-08-19 10:34:56', '', NULL, NULL);
INSERT INTO sys_dict_data VALUES ( 2, '草稿', '2', 'sys_article_status', NULL, NULL, NULL, '0', 'admin', '2021-08-19 10:35:06', '', NULL, NULL);

INSERT INTO sys_dict_data VALUES (1, '中文', 'zh-cn',  'sys_lang_type', NULL, NULL, NULL, '0', 'admin', '2021-08-19 10:35:06', '', NULL, NULL);
INSERT INTO sys_dict_data VALUES (2, '英文', 'en',     'sys_lang_type', NULL, NULL, NULL, '0', 'admin', '2021-08-19 10:35:06', '', NULL, NULL);
INSERT INTO sys_dict_data VALUES (3, '繁体', 'zh-tw',  'sys_lang_type', NULL, NULL, NULL, '0', 'admin', '2021-08-19 10:35:06', '', NULL, NULL);

GO

SET IDENTITY_INSERT sys_menu ON
-- 一级菜单
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, menuName_key) VALUES (1, '系统管理', 0, 1, 'system', NULL, 0, 0, 'M', '0', '0', '', 'system', '', GETDATE(), 'menu.system');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, menuName_key) VALUES (2, '系统监控', 0, 2, 'monitor', NULL, 0, 0, 'M', '0', '0', '', 'monitor', '', GETDATE(), 'menu.monitoring');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, menuName_key) VALUES (3, '系统工具', 0, 3, 'tool', NULL, 0, 0, 'M', '0', '0', '', 'tool', '', GETDATE(), 'menu.systemTools');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, menuName_key) VALUES (5, '外部打开', 0, 5, 'http://www.izhaorui.cn', NULL, 1, 0, 'M', '0', '0', '', 'link', '', GETDATE(), 'menu.officialWebsite');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, menuName_key) VALUES (6, '控制台',   0, 0, 'dashboard', 'index_v1', 0, 0, 'C', '0', '0', '', 'dashboard', '', GETDATE(), 'menu.dashboard');

-- 二级菜单
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, menuName_key) VALUES (100, '用户管理', 1, 1, 'user',		'system/user/index', 0, 0, 'C', '0', '0', 'system:user:list', 'user', '', GETDATE(), 'menu.systemUser');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, menuName_key) VALUES (101, '角色管理', 1, 2, 'role',		'system/role/index', 0, 0, 'C', '0', '0', 'system:role:list', 'peoples', '', GETDATE(), 'menu.systemRole');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, menuName_key) VALUES (102, '菜单管理', 1, 3, 'menu',		'system/menu/index', 0, 0, 'C', '0', '0', 'system:menu:list', 'tree-table', '', GETDATE(), 'menu.systemMenu');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, menuName_key) VALUES (103, '部门管理', 1, 4, 'dept',		'system/dept/index', 0, 0, 'C', '0', '0', 'system:dept:list', 'tree', '', GETDATE(), 'menu.systemDept');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, menuName_key) VALUES (104, '岗位管理', 1, 5, 'post',		'system/post/index', 0, 0, 'C', '0', '0', 'system:post:list', 'post', '', GETDATE(), 'menu.systemPost');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, menuName_key) VALUES (105, '字典管理', 1, 6, 'dict',		'system/dict/index', 0, 0, 'C', '0', '0', 'system:dict:list', 'dict', '', GETDATE(), 'menu.systemDic');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, menuName_key) VALUES (106, '角色分配', 1, 2, 'roleusers','system/roleusers/index', 0, 0, 'C', '1', '0', 'system:roleusers:list', 'people', '', GETDATE(), '');
INSERT into sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, menuName_key) VALUES (107, '参数设置', 1, 8, 'config',	'system/config/index', 0, 0, 'C', '0', '0', 'system:config:list','edit','', GETDATE(), 'menu.systemParam');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, menuName_key) VALUES (108, '日志管理', 1, 10, 'log', '', 0, 0, 'M', '0', '0', '', 'log', '', GETDATE(), 'menu.systemLog');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, menuName_key) VALUES (109, '通知公告', 1, 9, 'notice', 'system/notice/index', 0, 0, 'C', '0', '0', 'system:notice:list', 'message', '', GETDATE(), 'menu.systemNotice');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, menuName_key) VALUES (110, '定时任务', 2, 1, 'job', 'monitor/job/index', 0, 0, 'C', '0', '0', '', '', '', GETDATE(), 'menu.timedTask');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, menuName_key) VALUES (111, '在线用户', 2, 2, 'onlineusers', 'monitor/onlineuser/index', 0, 0, 'C', '0', '0', '', 'online', '', GETDATE(), 'layout.onlineUsers');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, menuName_key) VALUES (112, '服务监控', 2, 4, 'server', 'monitor/server/index', 0, 0, 'C', '0', '0', 'monitor:server:list', 'server', '', GETDATE(), 'menu.serviceMonitor');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, menuName_key) VALUES (113, '缓存监控', 2, 5, 'cache', 'monitor/cache/index', 0, 0, 'C', '1', '1', 'monitor:cache:list', 'redis', '', GETDATE(), 'menu.cacheMonitor');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, menuName_key) VALUES (114, '表单构建', 3, 1, 'build', 'tool/build/index', 0, 0, 'C', '0', '0', 'tool:build:list', 'build', '', GETDATE(), 'menu.formBuild');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, menuName_key) VALUES (115, '代码生成', 3, 2, 'gen', 'tool/gen/index', 0, 0, 'C', '0', '0', 'tool:gen:list', 'code', '', GETDATE(), 'menu.codeGeneration');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, menuName_key) VALUES (116, '系统接口', 3, 3, 'swagger', 'tool/swagger/index', 0, 0, 'C', '0', '0', 'tool:swagger:list', 'swagger', '', GETDATE(), 'menu.systemInterface');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, menuName_key) VALUES (117, '发送邮件', 3, 4, 'sendEmail', 'tool/email/sendEmail', 0, 0, 'C', '0', '0', 'tool:email:send', 'email', '', GETDATE(), 'menu.sendEmail');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, menuName_key) VALUES (118, '文章管理', 3, 18, 'article', NULL, 0, 0, 'M', '0', '0', NULL, 'documentation', '', GETDATE(), 'menu.systemArticle');


INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, menuName_key) VALUES (1047, '发布文章', 3  , 2, '/article/publish', 'system/article/publish', 0, 0, 'C', '1', '0', 'system:article:publish', 'log', '', GETDATE(), '');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, menuName_key) VALUES (119, '文章列表', 118, 1, 'index', 'system/article/manager', 0, 0, 'C', '0', '0', 'system:article:list', 'list', '', GETDATE(), 'menu.articleList');
-- 三级菜单日志管理
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, menuName_key) VALUES (500, '操作日志', 108, 1, 'operlog', 'monitor/operlog/index', 0, 0, 'C', '0', '0', 'monitor:operlog:list', 'form', '', GETDATE(), 'menu.operLog');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, menuName_key) VALUES (501, '登录日志', 108, 2, 'logininfor', 'monitor/logininfor/index', 0, 0, 'C', '0', '0', 'monitor:logininfor:list', 'logininfor', '', GETDATE(), 'menu.loginLog');


-- 用户管理 按钮
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1001, '用户查询', 100, 1, '', '', 0, 0, 'F', '0', '0', 'system:user:query', '', '', GETDATE(), '', NULL, '');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1002, '用户添加', 100, 2, '', '', 0, 0, 'F', '0', '0', 'system:user:add', '', '', GETDATE(), '', NULL, '');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1003, '用户修改', 100, 3, '', '', 0, 0, 'F', '0', '0', 'system:user:edit', '', '', GETDATE(), '', NULL, NULL);
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1004, '用户删除', 100, 4, '', '', 0, 0, 'F', '0', '0', 'system:user:remove', '', '', GETDATE(), '', NULL, '');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1005, '用户导出', 100, 5, '', '', 0, 0, 'F', '0', '0', 'system:user:export', '#', '', GETDATE(), '', NULL, '');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1006, '用户导入', 100, 6, '', '', 0, 0, 'F', '0', '0', 'system:user:import', '#', '', GETDATE(), '', NULL, '');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1007, '重置密码', 100, 7, '', '', 0, 0, 'F', '0', '0', 'system:user:resetPwd', '#', '', GETDATE(), '', NULL, '');
-- 权限管理 按钮
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1008, '角色查询', 101, 1, '', '', 0, 0, 'F', '0', '0', 'system:role:query', '#', '', GETDATE(), '', NULL, '');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1009, '角色新增', 101, 2, '', '', 0, 0, 'F', '0', '0', 'system:role:add', '#', '', GETDATE(), '', NULL, '');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1010, '角色修改', 101, 3, '', '', 0, 0, 'F', '0', '0', 'system:role:edit', '#', '', GETDATE(), '', NULL, '');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1011, '角色删除', 101, 4, '', '', 0, 0, 'F', '0', '0', 'system:role:remove', '#', '', GETDATE(), '', NULL, '');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1012, '菜单授权', 101, 5, '', '', 0, 0, 'F', '0', '0', 'system:role:authorize', '#', '', GETDATE(), '', NULL, '');
-- 分配用户 按钮
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1029, '新增用户', 106, 1, '', '', 0, 0, 'F', '0', '0', 'system:roleusers:add', NULL, '', GETDATE(), '', NULL, NULL);
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1030, '删除用户', 106, 2, '', '', 0, 0, 'F', '0', '0', 'system:roleusers:del', NULL, '', GETDATE(), '', NULL, NULL);
-- 菜单管理 按钮
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1013, '菜单查询', 102, 1, '', '', 0, 0, 'F', '0', '0', 'system:menu:query', '#', '', GETDATE(), '', NULL, '');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1014, '菜单新增', 102, 2, '', '', 0, 0, 'F', '0', '0', 'system:menu:add', '#', '', GETDATE(), '', NULL, '');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1015, '菜单修改', 102, 3, '', '', 0, 0, 'F', '0', '0', 'system:menu:edit', '#', '', GETDATE(), '', NULL, '');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1016, '菜单删除', 102, 4, '', '', 0, 0, 'F', '0', '0', 'system:menu:remove', '#', '', GETDATE(), '', NULL, '');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1017, '修改排序', 102, 5, '', '', 0, 0, 'F', '0', '0', 'system:menu:changeSort', '', '', GETDATE(), '', NULL, NULL);
-- 部门管理 按钮
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1018, '部门查询', 103, 1, '', '', 0, 0, 'F', '0', '0', 'system:dept:query', '', '', GETDATE(), '', NULL, NULL);
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1019, '部门新增', 103, 2, '', '', 0, 0, 'F', '0', '0', 'system:dept:add', NULL, '', GETDATE(), '', NULL, NULL);
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1020, '部门修改', 103, 3, '', '', 0, 0, 'F', '0', '0', 'system:dept:update', NULL, '', GETDATE(), '', NULL, NULL);
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1021, '部门删除', 103, 4, '', '', 0, 0, 'F', '0', '0', 'system:dept:remove', NULL, '', GETDATE(), '', NULL, NULL);
-- 岗位管理 按钮
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1022, '岗位查询', 104, 1, '', '', 0, 0, 'F', '0', '0', 'system:post:list', NULL, '', GETDATE(), '', NULL, NULL);
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1023, '岗位添加', 104, 2, '', '', 0, 0, 'F', '0', '0', 'system:post:add', NULL, '', GETDATE(), '', NULL, NULL);
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1024, '岗位删除', 104, 3, '', '', 0, 0, 'F', '0', '0', 'system:post:remove', NULL, '', GETDATE(), '', NULL, NULL);
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1025, '岗位编辑', 104, 4, '', '', 0, 0, 'F', '0', '0', 'system:post:edit', '', '', GETDATE(), '', NULL, NULL);
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1070, '岗位导出', 104, 4, '', '', 0, 0, 'F', '0', '0', 'system:post:export', '', '', GETDATE(), '', NULL, NULL);
-- 字典管理 按钮
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1031, '字典查询', 105, 1, '', '', 0, 0, 'F', '0', '0', 'system:dict:query', NULL, '', GETDATE(), '', NULL, NULL);
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1026, '字典新增', 105, 1, '', '', 0, 0, 'F', '0', '0', 'system:dict:add', NULL, '', GETDATE(), '', NULL, NULL);
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1027, '字典修改', 105, 2, '', '', 0, 0, 'F', '0', '0', 'system:dict:edit', NULL, '', GETDATE(), '', NULL, NULL);
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1028, '字典删除', 105, 3, '', '', 0, 0, 'F', '0', '0', 'system:dict:remove', NULL, '', GETDATE(), '', NULL, NULL);
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1071, '字典导出', 105, 4, '', '', 0, 0, 'F', '0', '0', 'system:dict:export', NULL, '', GETDATE(), '', NULL, NULL);
-- 定时任务 按钮
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) values (1032, '任务查询', 110, 1, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:job:list', '#', 'admin', GETDATE(), '', null, '');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1033, '任务新增', 110, 2, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:job:add', '', '', GETDATE(), '', NULL, NULL);
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1034, '任务删除', 110, 3, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:job:delete', '', '', GETDATE(), '', NULL, NULL);
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1035, '任务修改', 110, 4, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:job:edit', '', '', GETDATE(), '', NULL, NULL);
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1036, '任务启动', 110, 5, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:job:start', '', '', GETDATE(), '', NULL, NULL);
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1037, '任务运行', 110, 7, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:job:run', NULL, '', GETDATE(), '', NULL, NULL);
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1038, '任务停止', 110, 8, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:job:stop', NULL, '', GETDATE(), '', NULL, NULL);
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1039, '任务日志', 2, 0, 'job/log', 'monitor/job/log', 0, 0, 'C', '1', '0', 'monitor:job:query', 'log', '', GETDATE(), '', NULL, NULL);
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1040, '任务导出', 110,10, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:job:export', '', '', GETDATE(), '', NULL, NULL);
-- 操作日志 按钮
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) values (1041, '操作查询', 500, 1, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:operlog:query',  '', '', GETDATE(), '', null, NULL);
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1042, '操作删除', 500, 2, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:operlog:remove', '', '', GETDATE(), '', NULL, NULL);
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1043, '操作日志导出', 500, 3, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:operlog:export', '', '', GETDATE(), '', NULL, NULL);
-- 登录日志 按钮
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) values (1044, '登录查询', 501, 1, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:logininfor:query',   '', 'admin', GETDATE(), '', null, '');
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1045, '登录删除', 501, 1, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:logininfor:remove', '', '', GETDATE(), '', NULL, NULL);
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1046, '登录日志导出', 501, 1, '#', NULL, 0, 0, 'F', '0', '0', 'monitor:logininfor:export', '', '', GETDATE(), '', NULL, NULL);
-- 文章管理 按钮
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1048, '文章新增', 118, 2, '#', NULL, 0, 0, 'F', '0', '0', 'system:article:add', '', '', GETDATE(), '', NULL, NULL);
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1049, '文章修改', 118, 3, '#', NULL, 0, 0, 'F', '0', '0', 'system:article:update', '', '', GETDATE(), '', NULL, NULL);
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1050, '文章删除', 118, 4, '#', NULL, 0, 0, 'F', '0', '0', 'system:article:delete', '', '', GETDATE(), '', NULL, NULL);
-- 通知公告 按钮
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time) VALUES (1051, '查询公告', 109, 1, '#', NULL, 0, 0, 'F', '0', '0', 'system:notice:query', '', '', GETDATE());
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time) VALUES (1052, '新增公告', 109, 2, '#', NULL, 0, 0, 'F', '0', '0', 'system:notice:add', '', '', GETDATE());
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time) VALUES (1053, '删除公告', 109, 3, '#', NULL, 0, 0, 'F', '0', '0', 'system:notice:delete', '', '', GETDATE());
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time) VALUES (1054, '修改公告', 109, 4, '#', NULL, 0, 0, 'F', '0', '0', 'system:notice:update', '', '', GETDATE());
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time) VALUES (1055, '导出公告', 109, 5, '#', NULL, 0, 0, 'F', '0', '0', 'system:notice:export', '', '', GETDATE());

-- 代码生成 按钮
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1060, '生成修改', 3,   1, '/gen/editTable', 'tool/gen/editTable', 0, 0, 'C', '1', '0', 'tool:gen:edit', '', '', GETDATE(), '', NULL, NULL);
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1061, '生成查询', 115, 2, '#', NULL, 0, 0, 'F', '0', '0', 'tool:gen:query',  '', '', GETDATE(), '', NULL, NULL);
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1062, '生成删除', 115, 1, '#', NULL, 0, 0, 'F', '0', '0', 'tool:gen:remove', '', '', GETDATE(), '', NULL, NULL);
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1063, '导入代码', 115, 1, '#', NULL, 0, 0, 'F', '0', '0', 'tool:gen:import', '', '', GETDATE(), '', NULL, NULL);
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1064, '生成代码', 115, 1, '#', NULL, 0, 0, 'F', '0', '0', 'tool:gen:code', '', '', GETDATE(), '', NULL, NULL);
INSERT INTO sys_menu(menuId, menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time, update_by, update_time, remark) VALUES (1065, '预览代码', 115, 1, '#', NULL, 0, 0, 'F', '0', '0', 'tool:gen:preview', '', '', GETDATE(), '', NULL, NULL);

SET IDENTITY_INSERT sys_menu OFF

GO
-- 文件存储菜单
INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by, create_time, remark, menuName_key) 
VALUES ('文件存储', 3, 17, 'file', 'tool/file/index', 0, 0, 'C', '0', '0', 'tool:file:list', 'upload', '', '', '文件存储菜单', 'menu.fileStorage');

-- 按钮父菜单id
DECLARE @fileMenuId INT = SCOPE_IDENTITY();

INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_time) 
VALUES ('查询', @fileMenuId, 1, '#', NULL, 0, 0, 'F', '0', '0', 'tool:file:query', '', '');

INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_time) 
VALUES ('新增', @fileMenuId, 2, '#', NULL, 0, 0, 'F', '0', '0', 'tool:file:add', '',  '');

INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_time) 
VALUES ('删除', @fileMenuId, 3, '#', NULL, 0, 0, 'F', '0', '0', 'tool:file:delete', '', '');

INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_time) 
VALUES ('修改', @fileMenuId, 4, '#', NULL, 0, 0, 'F', '0', '0', 'tool:file:update', '', '');

INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_time) 
VALUES ('导出', @fileMenuId, 5, '#', NULL, 0, 0, 'F', '0', '0', 'tool:file:export', '', '');
GO

-- 多语言配置菜单
INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by, create_time, menuName_key) 
VALUES ('多语言配置', 1, 999, 'CommonLang', 'system/commonLang/index', 0, 0, 'C', '0', '0', 'system:lang:list', 'language', 'system', GETDATE(), 'menu.systemLang');

-- 按钮父菜单id
DECLARE @menuId INT = @@identity

INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time) 
VALUES ('查询', @menuId, 1, '#', NULL, 0, 0, 'F', '0', '0', 'system:lang:query', '', 'system', GETDATE());

INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time) 
VALUES ('新增', @menuId, 2, '#', NULL, 0, 0, 'F', '0', '0', 'system:lang:add', '', 'system', GETDATE());

INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time) 
VALUES ('删除', @menuId, 3, '#', NULL, 0, 0, 'F', '0', '0', 'system:lang:delete', '', 'system', GETDATE());

INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time) 
VALUES ('修改', @menuId, 4, '#', NULL, 0, 0, 'F', '0', '0', 'system:lang:edit', '', 'system', GETDATE());

INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time) 
VALUES ('导出', @menuId, 5, '#', NULL, 0, 0, 'F', '0', '0', 'system:lang:export', '', 'system', GETDATE());
GO

-- 文章目录菜单
INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by, create_time) 
VALUES ('文章目录', 118, 999, 'ArticleCategory', 'system/article/articleCategory', 0, 0, 'C', '0', '0', 'articlecategory:list', 'tree-table', 'system', GETDATE());
-- 按钮父菜单id
DECLARE @cmenuId int = @@identity
INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time) 
VALUES ('查询', @cmenuId, 1, '#', NULL, 0, 0, 'F', '0', '0', 'articlecategory:query', '', 'system', GETDATE());

INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time) 
VALUES ('新增', @cmenuId, 2, '#', NULL, 0, 0, 'F', '0', '0', 'articlecategory:add', '', 'system', GETDATE());

INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time) 
VALUES ('删除', @cmenuId, 3, '#', NULL, 0, 0, 'F', '0', '0', 'articlecategory:delete', '', 'system', GETDATE());

INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time) 
VALUES ('修改', @cmenuId, 4, '#', NULL, 0, 0, 'F', '0', '0', 'articlecategory:edit', '', 'system', GETDATE());

INSERT INTO sys_menu(menuName, parentId, orderNum, path, component, isFrame, isCache, menuType, visible, status, perms, icon, create_by,create_time) 
VALUES ('导出', @cmenuId, 5, '#', NULL, 0, 0, 'F', '0', '0', 'articlecategory:export', '', 'system', GETDATE());
GO

-- ----------------------------
-- Records of sys_post
-- ----------------------------
INSERT INTO sys_post VALUES ('CEO', '董事长', 1, '0', '', GETDATE(), '', NULL, '');
INSERT INTO sys_post VALUES ('SE', '项目经理', 2, '0', '', GETDATE(), '', NULL, '');
INSERT INTO sys_post VALUES ('HR', '人力资源', 3, '0', '', GETDATE(), '', NULL, '');
INSERT INTO sys_post VALUES ('USER', '普通员工', 4, '0', '', GETDATE(), '', NULL, '');
INSERT INTO sys_post VALUES ('PM', '人事经理', 0, '0', NULL, GETDATE(), '', NULL, NULL);
INSERT INTO sys_post VALUES ('GM', '总经理', 0, '0', NULL, GETDATE(), '', NULL, NULL);
INSERT INTO sys_post VALUES ('COO', '首席运营官', 0, '0', NULL, GETDATE(), '', NULL, NULL);
INSERT INTO sys_post VALUES ('CFO', '首席财务官', 0, '0', NULL, GETDATE(), '', NULL, NULL);
INSERT INTO sys_post VALUES ( 'CTO', '首席技术官', 0, '0', NULL, GETDATE(), '', NULL, NULL);
INSERT INTO sys_post VALUES ( 'HRD', '人力资源总监', 0, '0', NULL, GETDATE(), '', NULL, NULL);
INSERT INTO sys_post VALUES ( 'VP', '副总裁', 0, '0', NULL, GETDATE(), '', NULL, NULL);
INSERT INTO sys_post VALUES ( 'OD', '运营总监', 0, '0', NULL, GETDATE(), '', NULL, NULL);
INSERT INTO sys_post VALUES ( 'MD', '市场总监', 0, '0', NULL, GETDATE(), '', NULL, NULL);

GO
-- ----------------------------
-- Records of sys_user
-- ----------------------------
INSERT INTO sys_user VALUES (0,		'admin',	'管理员', '0', '', '', '0', '', 'e10adc3949ba59abbe56e057f20f883e', '0', '0', '', NULL, '', NULL, '', NULL, '管理员');
INSERT INTO sys_user VALUES (0,		'user',		'普通用户',	 '0', '', '', '0', '', 'e10adc3949ba59abbe56e057f20f883e', '0', '0', '', NULL, '', NULL, '', NULL, '普通用户');

GO
-- ----------------------------
-- Records of sys_user_post
-- ----------------------------
INSERT INTO sys_user_post VALUES (1, 1);
GO

-- ----------------------------
-- Records of sys_role
-- ----------------------------
INSERT INTO sys_role VALUES ('超级管理员', 'admin', 1, 1, 1, 0, 0, 0, 'admin', GETDATE(), '', NULL, '超级管理员');
INSERT INTO sys_role VALUES ('普通角色', 'common', 2, 5, 1, 0, 0, 0, 'admin', GETDATE(), '',  NULL, '普通角色');

GO
-- ----------------------------
-- Records of sys_user_role
-- ----------------------------
INSERT INTO sys_user_role VALUES (1, 1);
INSERT INTO sys_user_role VALUES (2, 2);

GO
-- ----------------------------
-- Records of sys_role_menu
-- ----------------------------
INSERT INTO [dbo].[sys_role_menu]([role_id], [menu_id], [create_by], [create_time]) VALUES (2, 1, 'admin', '2021-12-20 22:03:36.130');
INSERT INTO [dbo].[sys_role_menu]([role_id], [menu_id], [create_by], [create_time]) VALUES (2, 3, 'admin', '2021-12-20 22:03:36.130');
INSERT INTO [dbo].[sys_role_menu]([role_id], [menu_id], [create_by], [create_time]) VALUES (2, 5, 'admin', '2021-12-20 22:03:36.130');
INSERT INTO [dbo].[sys_role_menu]([role_id], [menu_id], [create_by], [create_time]) VALUES (2, 6, 'admin', '2021-12-20 22:03:36.130');
INSERT INTO [dbo].[sys_role_menu]([role_id], [menu_id], [create_by], [create_time]) VALUES (2, 100, 'admin', '2021-12-20 22:03:36.130');
INSERT INTO [dbo].[sys_role_menu]([role_id], [menu_id], [create_by], [create_time]) VALUES (2, 101, 'admin', '2021-12-20 22:03:36.130');
INSERT INTO [dbo].[sys_role_menu]([role_id], [menu_id], [create_by], [create_time]) VALUES (2, 102, 'admin', '2021-12-20 22:03:36.130');
INSERT INTO [dbo].[sys_role_menu]([role_id], [menu_id], [create_by], [create_time]) VALUES (2, 103, 'admin', '2021-12-20 22:03:36.130');
INSERT INTO [dbo].[sys_role_menu]([role_id], [menu_id], [create_by], [create_time]) VALUES (2, 104, 'admin', '2021-12-20 22:03:36.130');
INSERT INTO [dbo].[sys_role_menu]([role_id], [menu_id], [create_by], [create_time]) VALUES (2, 106, 'admin', '2021-12-20 22:03:36.130');
INSERT INTO [dbo].[sys_role_menu]([role_id], [menu_id], [create_by], [create_time]) VALUES (2, 108, 'admin', '2021-12-20 22:03:36.130');
INSERT INTO [dbo].[sys_role_menu]([role_id], [menu_id], [create_by], [create_time]) VALUES (2, 109, 'admin', '2021-12-20 22:03:36.130');
INSERT INTO [dbo].[sys_role_menu]([role_id], [menu_id], [create_by], [create_time]) VALUES (2, 114, 'admin', '2021-12-20 22:03:36.130');
INSERT INTO [dbo].[sys_role_menu]([role_id], [menu_id], [create_by], [create_time]) VALUES (2, 500, 'admin', '2021-12-20 22:03:36.130');
INSERT INTO [dbo].[sys_role_menu]([role_id], [menu_id], [create_by], [create_time]) VALUES (2, 501, 'admin', '2021-12-20 22:03:36.130');
INSERT INTO [dbo].[sys_role_menu]([role_id], [menu_id], [create_by], [create_time]) VALUES (2, 1001, 'admin', '2021-12-20 22:03:36.130');
INSERT INTO [dbo].[sys_role_menu]([role_id], [menu_id], [create_by], [create_time]) VALUES (2, 1008, 'admin', '2021-12-20 22:03:36.130');
INSERT INTO [dbo].[sys_role_menu]([role_id], [menu_id], [create_by], [create_time]) VALUES (2, 1013, 'admin', '2021-12-20 22:03:36.130');
INSERT INTO [dbo].[sys_role_menu]([role_id], [menu_id], [create_by], [create_time]) VALUES (2, 1018, 'admin', '2021-12-20 22:03:36.130');
INSERT INTO [dbo].[sys_role_menu]([role_id], [menu_id], [create_by], [create_time]) VALUES (2, 1022, 'admin', '2021-12-20 22:03:36.130');
INSERT INTO [dbo].[sys_role_menu]([role_id], [menu_id], [create_by], [create_time]) VALUES (2, 1031, 'admin', '2021-12-20 22:03:36.130');
INSERT INTO [dbo].[sys_role_menu]([role_id], [menu_id], [create_by], [create_time]) VALUES (2, 1041, 'admin', '2021-12-20 22:03:36.130');
INSERT INTO [dbo].[sys_role_menu]([role_id], [menu_id], [create_by], [create_time]) VALUES (2, 1044, 'admin', '2021-12-20 22:03:36.130');
INSERT INTO [dbo].[sys_role_menu]([role_id], [menu_id], [create_by], [create_time]) VALUES (2, 1051, 'admin', '2021-12-20 22:03:36.130');
GO

-- ----------------------------
-- Records of articleCategory
-- ----------------------------
INSERT INTO articleCategory VALUES ('C#', GETDATE(), 0);
INSERT INTO articleCategory VALUES ('java', GETDATE(), 0);
INSERT INTO articleCategory VALUES ('前端', GETDATE(), 0);
INSERT INTO articleCategory VALUES ('数据库', GETDATE(), 0);
INSERT INTO articleCategory VALUES ('其他', GETDATE(), 0);
INSERT INTO articleCategory VALUES ('c++', GETDATE(), 5);
INSERT INTO articleCategory VALUES ('vue', GETDATE(), 3);
INSERT INTO articleCategory VALUES ('sqlserver', GETDATE(), 4);
GO

insert into sys_config values('主框架页-默认皮肤样式名称',     'sys.index.skinName',            'skin-blue',     'Y', 'admin', GETDATE(), '', null, '蓝色 skin-blue、绿色 skin-green、紫色 skin-purple、红色 skin-red、黄色 skin-yellow' );
insert into sys_config values('用户管理-账号初始密码',         'sys.user.initPassword',         '123456',        'Y', 'admin', GETDATE(), '', null, '初始化密码 123456' );
insert into sys_config values('主框架页-侧边栏主题',           'sys.index.sideTheme',           'theme-dark',    'Y', 'admin', GETDATE(), '', null, '深色主题theme-dark，浅色主题theme-light' );
insert into sys_config values('账号自助-验证码开关',           'sys.account.captchaOnOff',      '1',          'Y', 'admin', GETDATE(), '', null, '开启验证码功能（off、关闭，1、动态验证码 2、动态gif泡泡 3、泡泡 4、静态验证码）');
INSERT INTO sys_config VALUES('本地文件上传访问域名', 		   'sys.file.uploadurl', 			'http://localhost:8888', 'Y', 'admin', GETDATE(), '', NULL, NULL);
INSERT INTO sys_config VALUES('开启注册功能', 		   		   'sys.account.register', 		 	'true', 		'Y', 'admin', GETDATE(), '', NULL, NULL);
INSERT INTO sys_config VALUES('文章预览地址', 		   		   'sys.article.preview.url', 		 	'http://www.izhaorui.cn/article/details/', 		'Y', 'admin', GETDATE(), '', NULL, '格式：http://www.izhaorui.cn/article/details/{aid}，其中{aid}为文章的id');

GO


SELECT * FROM dbo.sys_user
GO
SELECT * FROM dbo.sys_user_role
GO
SELECT * FROM dbo.sys_user_post
GO
SELECT * FROM dbo.sys_role
GO
SELECT * FROM dbo.sys_role_menu
GO
SELECT * FROM dbo.sys_dept
GO
SELECT * FROM dbo.sys_dict_type
GO
SELECT * FROM dbo.sys_dict_data
GO
SELECT * FROM dbo.sys_menu
GO
SELECT * FROM dbo.articleCategory
GO
SELECT * FROM dbo.sys_config

SELECT * FROM dbo.gen_table
SELECT * FROM dbo.gen_table_column
SELECT * FROM dbo.gen_demo
GO
--TRUNCATE TABLE gen_table_column
--TRUNCATE TABLE gen_table