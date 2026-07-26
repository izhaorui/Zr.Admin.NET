-- ============================================================
-- 工作流模块初始化脚本 (SQL Server)
-- 执行前请确认已连接到 ZrAdmin 业务库
-- ============================================================

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='wf_flow_definition' AND xtype='U')
CREATE TABLE wf_flow_definition (
    FlowId bigint IDENTITY(1,1) PRIMARY KEY,
    FlowCode nvarchar(64) NOT NULL,
    FlowName nvarchar(100) NOT NULL,
    FormType int NOT NULL DEFAULT 0,
    Status int NOT NULL DEFAULT 1,
    is_delete int NOT NULL DEFAULT 0,
    Create_by nvarchar(64) NULL,
    Create_time datetime NULL,
    Update_by nvarchar(64) NULL,
    Update_time datetime NULL,
    Remark nvarchar(500) NULL
);

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='wf_flow_node' AND xtype='U')
CREATE TABLE wf_flow_node (
    NodeId bigint IDENTITY(1,1) PRIMARY KEY,
    FlowId bigint NOT NULL,
    NodeName nvarchar(100) NOT NULL,
    NodeType int NOT NULL DEFAULT 1,
    ApproverType int NOT NULL DEFAULT 0,
    ApproverId nvarchar(500) NULL,
    NodeOrder int NOT NULL DEFAULT 1,
    SignType int NOT NULL DEFAULT 0,
    Create_by nvarchar(64) NULL,
    Create_time datetime NULL,
    Update_by nvarchar(64) NULL,
    Update_time datetime NULL,
    Remark nvarchar(500) NULL
);

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='wf_flow_instance' AND xtype='U')
CREATE TABLE wf_flow_instance (
    InstanceId bigint IDENTITY(1,1) PRIMARY KEY,
    FlowId bigint NOT NULL,
    FlowName nvarchar(100) NULL,
    BusinessKey nvarchar(100) NULL,
    Title nvarchar(200) NOT NULL,
    ApplyUser nvarchar(64) NOT NULL,
    Status int NOT NULL DEFAULT 0,
    CurrentNodeId bigint NULL,
    FormContent nvarchar(max) NULL,
    Attachment nvarchar(1000) NULL,
    Create_by nvarchar(64) NULL,
    Create_time datetime NULL,
    Update_by nvarchar(64) NULL,
    Update_time datetime NULL,
    Remark nvarchar(500) NULL
);

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='wf_flow_task' AND xtype='U')
CREATE TABLE wf_flow_task (
    TaskId bigint IDENTITY(1,1) PRIMARY KEY,
    InstanceId bigint NOT NULL,
    NodeId bigint NOT NULL,
    NodeName nvarchar(100) NULL,
    Assignee nvarchar(64) NOT NULL,
    Status int NOT NULL DEFAULT 0,
    Opinion nvarchar(500) NULL,
    Action int NULL,
    HandleTime datetime NULL,
    Create_by nvarchar(64) NULL,
    Create_time datetime NULL,
    Update_by nvarchar(64) NULL,
    Update_time datetime NULL,
    Remark nvarchar(500) NULL
);

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='wf_flow_record' AND xtype='U')
CREATE TABLE wf_flow_record (
    RecordId bigint IDENTITY(1,1) PRIMARY KEY,
    TaskId bigint NULL,
    InstanceId bigint NOT NULL,
    NodeId bigint NULL,
    Operator nvarchar(64) NOT NULL,
    Action int NOT NULL DEFAULT 0,
    Opinion nvarchar(500) NULL,
    Create_by nvarchar(64) NULL,
    Create_time datetime NULL,
    Update_by nvarchar(64) NULL,
    Update_time datetime NULL,
    Remark nvarchar(500) NULL
);

CREATE INDEX IX_wf_flow_node_flowid ON wf_flow_node(FlowId);
CREATE INDEX IX_wf_flow_instance_applyuser ON wf_flow_instance(ApplyUser);
CREATE INDEX IX_wf_flow_instance_status ON wf_flow_instance(Status);
CREATE INDEX IX_wf_flow_instance_currentnode ON wf_flow_instance(CurrentNodeId);
CREATE INDEX IX_wf_flow_task_assignee ON wf_flow_task(Assignee);
CREATE INDEX IX_wf_flow_task_status ON wf_flow_task(Status);
CREATE INDEX IX_wf_flow_task_instanceid ON wf_flow_task(InstanceId);
CREATE INDEX IX_wf_flow_record_instanceid ON wf_flow_record(InstanceId);

-- 流程定义表单字段（轻量动态表单 JSON；方案2 可升级为可视化设计器 schema）
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name='form_items' AND object_id = OBJECT_ID('wf_flow_definition'))
ALTER TABLE wf_flow_definition ADD form_items nvarchar(max) NULL;

-- 软删除标记（0=未删 1=已删）；保留节点/实例/任务/记录等历史数据
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name='is_delete' AND object_id = OBJECT_ID('wf_flow_definition'))
BEGIN
    ALTER TABLE wf_flow_definition ADD is_delete int NOT NULL DEFAULT 0;
    CREATE INDEX IX_wf_flow_definition_isdelete ON wf_flow_definition(is_delete);
END
