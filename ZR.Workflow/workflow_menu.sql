-- ============================================================
-- 工作流模块菜单初始化 (SQL Server)
-- 插入到 sys_menu，前端路由由后端 getRouters 动态加载
-- 执行前请确认已连接到 ZrAdmin 业务库
-- ============================================================

-- 工作流（目录）
INSERT INTO sys_menu (MenuName, ParentId, OrderNum, Path, Component, IsCache, IsFrame, MenuType, Visible, Status, Perms, Icon, RouteName, Create_by, Create_time)
VALUES ('工作流', 0, 50, 'workflow', '', '0', '0', 'M', '0', '0', '', 's-order', 'Workflow', 'admin', GETDATE());
DECLARE @wfDir BIGINT = SCOPE_IDENTITY();

-- 流程定义（菜单）
INSERT INTO sys_menu (MenuName, ParentId, OrderNum, Path, Component, IsCache, IsFrame, MenuType, Visible, Status, Perms, Icon, RouteName, Create_by, Create_time)
VALUES ('流程定义', @wfDir, 1, 'definition', 'workflow/flowDefinition/index', '0', '0', 'C', '0', '0', 'workflow:definition:list', 's-operation', '', 'admin', GETDATE());
DECLARE @def BIGINT = SCOPE_IDENTITY();

INSERT INTO sys_menu (MenuName, ParentId, OrderNum, Path, Component, IsCache, IsFrame, MenuType, Visible, Status, Perms, Icon, RouteName, Create_by, Create_time)
VALUES ('新增', @def, 1, '', '', '0', '0', 'F', '0', '0', 'workflow:definition:add', '#', '', 'admin', GETDATE());
INSERT INTO sys_menu (MenuName, ParentId, OrderNum, Path, Component, IsCache, IsFrame, MenuType, Visible, Status, Perms, Icon, RouteName, Create_by, Create_time)
VALUES ('修改', @def, 2, '', '', '0', '0', 'F', '0', '0', 'workflow:definition:edit', '#', '', 'admin', GETDATE());
INSERT INTO sys_menu (MenuName, ParentId, OrderNum, Path, Component, IsCache, IsFrame, MenuType, Visible, Status, Perms, Icon, RouteName, Create_by, Create_time)
VALUES ('删除', @def, 3, '', '', '0', '0', 'F', '0', '0', 'workflow:definition:delete', '#', '', 'admin', GETDATE());

-- 我发起的（菜单）
INSERT INTO sys_menu (MenuName, ParentId, OrderNum, Path, Component, IsCache, IsFrame, MenuType, Visible, Status, Perms, Icon, RouteName, Create_by, Create_time)
VALUES ('我发起的', @wfDir, 2, 'my', 'workflow/instance/index', '0', '0', 'C', '0', '0', 'workflow:instance:list', 's-promotion', '', 'admin', GETDATE());
DECLARE @my BIGINT = SCOPE_IDENTITY();

INSERT INTO sys_menu (MenuName, ParentId, OrderNum, Path, Component, IsCache, IsFrame, MenuType, Visible, Status, Perms, Icon, RouteName, Create_by, Create_time)
VALUES ('发起', @my, 1, '', '', '0', '0', 'F', '0', '0', 'workflow:instance:start', '#', '', 'admin', GETDATE());
INSERT INTO sys_menu (MenuName, ParentId, OrderNum, Path, Component, IsCache, IsFrame, MenuType, Visible, Status, Perms, Icon, RouteName, Create_by, Create_time)
VALUES ('撤回', @my, 2, '', '', '0', '0', 'F', '0', '0', 'workflow:instance:withdraw', '#', '', 'admin', GETDATE());

-- 待我审批（菜单）
INSERT INTO sys_menu (MenuName, ParentId, OrderNum, Path, Component, IsCache, IsFrame, MenuType, Visible, Status, Perms, Icon, RouteName, Create_by, Create_time)
VALUES ('待我审批', @wfDir, 3, 'todo', 'workflow/todo/index', '0', '0', 'C', '0', '0', 'workflow:task:list', 's-check', '', 'admin', GETDATE());
DECLARE @todo BIGINT = SCOPE_IDENTITY();

INSERT INTO sys_menu (MenuName, ParentId, OrderNum, Path, Component, IsCache, IsFrame, MenuType, Visible, Status, Perms, Icon, RouteName, Create_by, Create_time)
VALUES ('通过', @todo, 1, '', '', '0', '0', 'F', '0', '0', 'workflow:task:approve', '#', '', 'admin', GETDATE());
INSERT INTO sys_menu (MenuName, ParentId, OrderNum, Path, Component, IsCache, IsFrame, MenuType, Visible, Status, Perms, Icon, RouteName, Create_by, Create_time)
VALUES ('驳回', @todo, 2, '', '', '0', '0', 'F', '0', '0', 'workflow:task:reject', '#', '', 'admin', GETDATE());

-- 已办任务（菜单）
INSERT INTO sys_menu (MenuName, ParentId, OrderNum, Path, Component, IsCache, IsFrame, MenuType, Visible, Status, Perms, Icon, RouteName, Create_by, Create_time)
VALUES ('已办任务', @wfDir, 4, 'done', 'workflow/done/index', '0', '0', 'C', '0', '0', 'workflow:task:list', 's-checked', '', 'admin', GETDATE());

-- 审批记录（菜单）
INSERT INTO sys_menu (MenuName, ParentId, OrderNum, Path, Component, IsCache, IsFrame, MenuType, Visible, Status, Perms, Icon, RouteName, Create_by, Create_time)
VALUES ('审批记录', @wfDir, 5, 'record', 'workflow/record/index', '0', '0', 'C', '0', '0', 'workflow:record:list', 's-data', '', 'admin', GETDATE());

-- 抄送给我（菜单）：复用前面已声明的 @wfDir 作为父目录
INSERT INTO sys_menu (MenuName, ParentId, OrderNum, Path, Component, IsCache, IsFrame, MenuType, Visible, Status, Perms, Icon, RouteName, Create_by, Create_time)
VALUES ('抄送给我', @wfDir, 6, 'cc', 'workflow/cc/index', '0', '0', 'C', '0', '0', 'workflow:record:cc', 's-comment', 'WfCc', 'admin', GETDATE());
DECLARE @cc BIGINT = SCOPE_IDENTITY();

INSERT INTO sys_menu (MenuName, ParentId, OrderNum, Path, Component, IsCache, IsFrame, MenuType, Visible, Status, Perms, Icon, RouteName, Create_by, Create_time)
VALUES ('查看', @cc, 1, '', '', '0', '0', 'F', '0', '0', 'workflow:record:cc', '#', '', 'admin', GETDATE());

-- 待办任务的转办/加签权限（按钮）
INSERT INTO sys_menu (MenuName, ParentId, OrderNum, Path, Component, IsCache, IsFrame, MenuType, Visible, Status, Perms, Icon, RouteName, Create_by, Create_time)
VALUES ('转办', @todo, 3, '', '', '0', '0', 'F', '0', '0', 'workflow:task:transfer', '#', '', 'admin', GETDATE());
INSERT INTO sys_menu (MenuName, ParentId, OrderNum, Path, Component, IsCache, IsFrame, MenuType, Visible, Status, Perms, Icon, RouteName, Create_by, Create_time)
VALUES ('加签', @todo, 4, '', '', '0', '0', 'F', '0', '0', 'workflow:task:addsign', '#', '', 'admin', GETDATE());

-- 数据面板（菜单）：挂到工作流目录，OrderNum=0 排在最前；可重复执行
IF NOT EXISTS (SELECT 1 FROM sys_menu WHERE RouteName = 'WfDashboard')
BEGIN
    DECLARE @wfDash BIGINT = (SELECT TOP 1 MenuId FROM sys_menu WHERE MenuName = '工作流' AND ParentId = 0);
    INSERT INTO sys_menu (MenuName, ParentId, OrderNum, Path, Component, IsCache, IsFrame, MenuType, Visible, Status, Perms, Icon, RouteName, Create_by, Create_time)
    VALUES ('数据面板', @wfDash, 0, 'dashboard', 'workflow/dashboard/index', '0', '0', 'C', '0', '0', 'workflow:instance:list', 's-data', 'WfDashboard', 'admin', GETDATE());
END
