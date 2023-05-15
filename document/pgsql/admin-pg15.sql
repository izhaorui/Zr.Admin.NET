/*
 Navicat Premium Data Transfer
 
 Source Server Type    : PostgreSQL
 Source Server Version : 150001 (150001)  
 Source Schema         : public

 Target Server Type    : PostgreSQL
 Target Server Version : 150001 (150001)
 File Encoding         : 65001

 Date: 20/12/2022 16:07:00
*/


-- ----------------------------
-- Sequence structure for articlecategoryid_seq
-- ----------------------------
DROP SEQUENCE IF EXISTS "public"."articlecategoryid_seq";
CREATE SEQUENCE "public"."articlecategoryid_seq" 
INCREMENT 1
MINVALUE  1
MAXVALUE 999999999999
START 1
CACHE 1;

-- ----------------------------
-- Sequence structure for articleid
-- ----------------------------
DROP SEQUENCE IF EXISTS "public"."articleid";
CREATE SEQUENCE "public"."articleid" 
INCREMENT 1
MINVALUE  1
MAXVALUE 999999999999
START 1
CACHE 1;

-- ----------------------------
-- Sequence structure for gen_demoid_seq
-- ----------------------------
DROP SEQUENCE IF EXISTS "public"."gen_demoid_seq";
CREATE SEQUENCE "public"."gen_demoid_seq" 
INCREMENT 1
MINVALUE  1
MAXVALUE 999999999999
START 1
CACHE 1;

-- ----------------------------
-- Sequence structure for gen_table_columnid_seq
-- ----------------------------
DROP SEQUENCE IF EXISTS "public"."gen_table_columnid_seq";
CREATE SEQUENCE "public"."gen_table_columnid_seq" 
INCREMENT 1
MINVALUE  1
MAXVALUE 999999999999
START 1
CACHE 1;

-- ----------------------------
-- Sequence structure for gen_tableid_seq
-- ----------------------------
DROP SEQUENCE IF EXISTS "public"."gen_tableid_seq";
CREATE SEQUENCE "public"."gen_tableid_seq" 
INCREMENT 1
MINVALUE  1
MAXVALUE 999999999999
START 1
CACHE 1;

-- ----------------------------
-- Sequence structure for sys_configid_seq
-- ----------------------------
DROP SEQUENCE IF EXISTS "public"."sys_configid_seq";
CREATE SEQUENCE "public"."sys_configid_seq" 
INCREMENT 1
MINVALUE  1
MAXVALUE 999999999999
START 1
CACHE 1;

-- ----------------------------
-- Sequence structure for sys_deptid_seq
-- ----------------------------
DROP SEQUENCE IF EXISTS "public"."sys_deptid_seq";
CREATE SEQUENCE "public"."sys_deptid_seq" 
INCREMENT 1
MINVALUE  1
MAXVALUE 999999999999
START 1
CACHE 1;

-- ----------------------------
-- Sequence structure for sys_dict_dataid_seq
-- ----------------------------
DROP SEQUENCE IF EXISTS "public"."sys_dict_dataid_seq";
CREATE SEQUENCE "public"."sys_dict_dataid_seq" 
INCREMENT 1
MINVALUE  1
MAXVALUE 999999999999
START 1
CACHE 1;

-- ----------------------------
-- Sequence structure for sys_dict_typeid_seq
-- ----------------------------
DROP SEQUENCE IF EXISTS "public"."sys_dict_typeid_seq";
CREATE SEQUENCE "public"."sys_dict_typeid_seq" 
INCREMENT 1
MINVALUE  1
MAXVALUE 999999999999
START 1
CACHE 1;

-- ----------------------------
-- Sequence structure for sys_logininforid_seq
-- ----------------------------
DROP SEQUENCE IF EXISTS "public"."sys_logininforid_seq";
CREATE SEQUENCE "public"."sys_logininforid_seq" 
INCREMENT 1
MINVALUE  1
MAXVALUE 999999999999
START 1
CACHE 1;

-- ----------------------------
-- Sequence structure for sys_menuid_seq
-- ----------------------------
DROP SEQUENCE IF EXISTS "public"."sys_menuid_seq";
CREATE SEQUENCE "public"."sys_menuid_seq" 
INCREMENT 1
MINVALUE  1
MAXVALUE 999999999999
START 1
CACHE 1;

-- ----------------------------
-- Sequence structure for sys_noticeid_seq
-- ----------------------------
DROP SEQUENCE IF EXISTS "public"."sys_noticeid_seq";
CREATE SEQUENCE "public"."sys_noticeid_seq" 
INCREMENT 1
MINVALUE  1
MAXVALUE 999999999999
START 1
CACHE 1;

-- ----------------------------
-- Sequence structure for sys_oper_logid_seq
-- ----------------------------
DROP SEQUENCE IF EXISTS "public"."sys_oper_logid_seq";
CREATE SEQUENCE "public"."sys_oper_logid_seq" 
INCREMENT 1
MINVALUE  1
MAXVALUE 999999999999
START 1
CACHE 1;

-- ----------------------------
-- Sequence structure for sys_postid_seq
-- ----------------------------
DROP SEQUENCE IF EXISTS "public"."sys_postid_seq";
CREATE SEQUENCE "public"."sys_postid_seq" 
INCREMENT 1
MINVALUE  1
MAXVALUE 999999999999
START 1
CACHE 1;

-- ----------------------------
-- Sequence structure for sys_roleid_seq
-- ----------------------------
DROP SEQUENCE IF EXISTS "public"."sys_roleid_seq";
CREATE SEQUENCE "public"."sys_roleid_seq" 
INCREMENT 1
MINVALUE  1
MAXVALUE 999999999999
START 1
CACHE 1;

-- ----------------------------
-- Sequence structure for sys_tasks_logid_seq
-- ----------------------------
DROP SEQUENCE IF EXISTS "public"."sys_tasks_logid_seq";
CREATE SEQUENCE "public"."sys_tasks_logid_seq" 
INCREMENT 1
MINVALUE  1
MAXVALUE 999999999999
START 1
CACHE 1;

-- ----------------------------
-- Sequence structure for sys_userid_seq
-- ----------------------------
DROP SEQUENCE IF EXISTS "public"."sys_userid_seq";
CREATE SEQUENCE "public"."sys_userid_seq" 
INCREMENT 1
MINVALUE  1
MAXVALUE 999999999999
START 4
CACHE 1;

-- ----------------------------
-- Table structure for article
-- ----------------------------
DROP TABLE IF EXISTS "public"."article";
CREATE TABLE "public"."article" (
  "cid" int4 NOT NULL DEFAULT nextval('articleid'::regclass),
  "title" varchar(254) COLLATE "pg_catalog"."default",
  "content" text COLLATE "pg_catalog"."default",
  "userid" int8,
  "status" varchar(20) COLLATE "pg_catalog"."default",
  "fmt_type" varchar(20) COLLATE "pg_catalog"."default",
  "tags" varchar(100) COLLATE "pg_catalog"."default",
  "hits" int4,
  "category_id" int4,
  "createtime" timestamp(6),
  "updatetime" timestamp(6),
  "authorname" varchar(20) COLLATE "pg_catalog"."default",
  "coverurl" varchar(255) COLLATE "pg_catalog"."default"
)
;
COMMENT ON COLUMN "public"."article"."title" IS '文章标题';
COMMENT ON COLUMN "public"."article"."content" IS '文章内容';
COMMENT ON COLUMN "public"."article"."userid" IS '用户id';
COMMENT ON COLUMN "public"."article"."status" IS '文章状态1、已发布 2、草稿';
COMMENT ON COLUMN "public"."article"."fmt_type" IS '编辑器类型markdown,html';
COMMENT ON COLUMN "public"."article"."tags" IS '文章标签';
COMMENT ON COLUMN "public"."article"."hits" IS '点击量';
COMMENT ON COLUMN "public"."article"."category_id" IS '目录id';
COMMENT ON COLUMN "public"."article"."createtime" IS '创建时间';
COMMENT ON COLUMN "public"."article"."updatetime" IS '修改时间';
COMMENT ON COLUMN "public"."article"."authorname" IS '作者名';
COMMENT ON COLUMN "public"."article"."coverurl" IS '封面';

-- ----------------------------
-- Records of article
-- ----------------------------

-- ----------------------------
-- Table structure for articlecategory
-- ----------------------------
DROP TABLE IF EXISTS "public"."articlecategory";
CREATE TABLE "public"."articlecategory" (
  "category_id" int4 NOT NULL DEFAULT nextval('articlecategoryid_seq'::regclass),
  "name" varchar(20) COLLATE "pg_catalog"."default" NOT NULL,
  "create_time" timestamp(6),
  "parentid" int8
)
;
COMMENT ON COLUMN "public"."articlecategory"."category_id" IS '目录id';
COMMENT ON COLUMN "public"."articlecategory"."name" IS '目录名';
COMMENT ON COLUMN "public"."articlecategory"."create_time" IS '创建时间';
COMMENT ON COLUMN "public"."articlecategory"."parentid" IS '父级ID';

-- ----------------------------
-- Records of articlecategory
-- ----------------------------

-- ----------------------------
-- Table structure for gen_demo
-- ----------------------------
DROP TABLE IF EXISTS "public"."gen_demo";
CREATE TABLE "public"."gen_demo" (
  "id" int4 NOT NULL DEFAULT nextval('gen_demoid_seq'::regclass),
  "name" varchar(20) COLLATE "pg_catalog"."default" NOT NULL,
  "icon" varchar(255) COLLATE "pg_catalog"."default",
  "showstatus" int4 NOT NULL,
  "addtime" timestamp(6),
  "sex" int4,
  "sort" int4,
  "remark" varchar(200) COLLATE "pg_catalog"."default",
  "begintime" timestamp(6),
  "endtime" timestamp(6),
  "feature" varchar(100) COLLATE "pg_catalog"."default"
)
;
COMMENT ON COLUMN "public"."gen_demo"."id" IS '自增id';
COMMENT ON COLUMN "public"."gen_demo"."name" IS '名称';
COMMENT ON COLUMN "public"."gen_demo"."icon" IS '图片';
COMMENT ON COLUMN "public"."gen_demo"."showstatus" IS '显示状态';
COMMENT ON COLUMN "public"."gen_demo"."addtime" IS '添加时间';
COMMENT ON COLUMN "public"."gen_demo"."sex" IS '用户性别';
COMMENT ON COLUMN "public"."gen_demo"."sort" IS '排序';
COMMENT ON COLUMN "public"."gen_demo"."remark" IS '备注';
COMMENT ON COLUMN "public"."gen_demo"."begintime" IS '开始时间';
COMMENT ON COLUMN "public"."gen_demo"."endtime" IS '结束时间';
COMMENT ON COLUMN "public"."gen_demo"."feature" IS '特征';

-- ----------------------------
-- Records of gen_demo
-- ----------------------------

-- ----------------------------
-- Table structure for gen_table
-- ----------------------------
DROP TABLE IF EXISTS "public"."gen_table";
CREATE TABLE "public"."gen_table" (
  "tableid" int8 NOT NULL DEFAULT nextval('gen_tableid_seq'::regclass),
  "tablename" varchar(200) COLLATE "pg_catalog"."default",
  "tablecomment" varchar(500) COLLATE "pg_catalog"."default",
  "subtablename" varchar(64) COLLATE "pg_catalog"."default",
  "subtablefkname" varchar(64) COLLATE "pg_catalog"."default",
  "classname" varchar(100) COLLATE "pg_catalog"."default",
  "tplcategory" varchar(200) COLLATE "pg_catalog"."default",
  "basenamespace" varchar(100) COLLATE "pg_catalog"."default",
  "modulename" varchar(30) COLLATE "pg_catalog"."default",
  "businessname" varchar(30) COLLATE "pg_catalog"."default",
  "functionname" varchar(50) COLLATE "pg_catalog"."default",
  "functionauthor" varchar(50) COLLATE "pg_catalog"."default",
  "gentype" char(1) COLLATE "pg_catalog"."default",
  "genpath" varchar(200) COLLATE "pg_catalog"."default",
  "options" varchar(1000) COLLATE "pg_catalog"."default",
  "create_by" varchar(64) COLLATE "pg_catalog"."default",
  "create_time" timestamp(6),
  "update_by" varchar(64) COLLATE "pg_catalog"."default",
  "update_time" timestamp(6),
  "remark" varchar(500) COLLATE "pg_catalog"."default",
  "dbname" varchar(100) COLLATE "pg_catalog"."default"
)
;
COMMENT ON COLUMN "public"."gen_table"."tableid" IS '编号';
COMMENT ON COLUMN "public"."gen_table"."tablename" IS '表名称';
COMMENT ON COLUMN "public"."gen_table"."tablecomment" IS '表描述';
COMMENT ON COLUMN "public"."gen_table"."subtablename" IS '关联子表的表名';
COMMENT ON COLUMN "public"."gen_table"."subtablefkname" IS '子表关联的外键名';
COMMENT ON COLUMN "public"."gen_table"."classname" IS '实体类名称';
COMMENT ON COLUMN "public"."gen_table"."tplcategory" IS '使用的模板（crud单表操作 tree树表操作）';
COMMENT ON COLUMN "public"."gen_table"."basenamespace" IS '生成命名空间前缀';
COMMENT ON COLUMN "public"."gen_table"."modulename" IS '生成模块名';
COMMENT ON COLUMN "public"."gen_table"."businessname" IS '生成业务名';
COMMENT ON COLUMN "public"."gen_table"."functionname" IS '生成功能名';
COMMENT ON COLUMN "public"."gen_table"."functionauthor" IS '生成功能作者';
COMMENT ON COLUMN "public"."gen_table"."gentype" IS '生成代码方式（0zip压缩包 1自定义路径）';
COMMENT ON COLUMN "public"."gen_table"."genpath" IS '生成路径（不填默认项目路径）';
COMMENT ON COLUMN "public"."gen_table"."options" IS '其它生成选项';
COMMENT ON COLUMN "public"."gen_table"."create_by" IS '创建者';
COMMENT ON COLUMN "public"."gen_table"."create_time" IS '创建时间';
COMMENT ON COLUMN "public"."gen_table"."update_by" IS '更新者';
COMMENT ON COLUMN "public"."gen_table"."update_time" IS '更新时间';
COMMENT ON COLUMN "public"."gen_table"."remark" IS '备注';
COMMENT ON COLUMN "public"."gen_table"."dbname" IS '数据库名';
COMMENT ON TABLE "public"."gen_table" IS '代码生成业务表';

-- ----------------------------
-- Records of gen_table
-- ----------------------------

-- ----------------------------
-- Table structure for gen_table_column
-- ----------------------------
DROP TABLE IF EXISTS "public"."gen_table_column";
CREATE TABLE "public"."gen_table_column" (
  "columnid" int8 NOT NULL DEFAULT nextval('gen_table_columnid_seq'::regclass),
  "tablename" varchar(200) COLLATE "pg_catalog"."default",
  "tableid" int8,
  "columnname" varchar(200) COLLATE "pg_catalog"."default",
  "columncomment" varchar(500) COLLATE "pg_catalog"."default",
  "columntype" varchar(100) COLLATE "pg_catalog"."default",
  "csharptype" varchar(100) COLLATE "pg_catalog"."default",
  "csharpfield" varchar(100) COLLATE "pg_catalog"."default",
  "ispk" int2,
  "isincrement" int2,
  "isrequired" int2,
  "isinsert" int2,
  "isedit" int2,
  "islist" int2,
  "isquery" int2,
  "issort" int2,
  "isexport" int2,
  "querytype" varchar(200) COLLATE "pg_catalog"."default",
  "htmltype" varchar(200) COLLATE "pg_catalog"."default",
  "dicttype" varchar(200) COLLATE "pg_catalog"."default",
  "sort" int4,
  "create_by" varchar(64) COLLATE "pg_catalog"."default",
  "create_time" timestamp(6),
  "update_by" varchar(64) COLLATE "pg_catalog"."default",
  "update_time" timestamp(6),
  "remark" varchar(200) COLLATE "pg_catalog"."default"
)
;
COMMENT ON COLUMN "public"."gen_table_column"."columnid" IS '编号';
COMMENT ON COLUMN "public"."gen_table_column"."tablename" IS '表名';
COMMENT ON COLUMN "public"."gen_table_column"."tableid" IS '归属表编号';
COMMENT ON COLUMN "public"."gen_table_column"."columnname" IS '列名称';
COMMENT ON COLUMN "public"."gen_table_column"."columncomment" IS '列描述';
COMMENT ON COLUMN "public"."gen_table_column"."columntype" IS '列类型';
COMMENT ON COLUMN "public"."gen_table_column"."csharptype" IS 'C#类型';
COMMENT ON COLUMN "public"."gen_table_column"."csharpfield" IS 'C#字段名';
COMMENT ON COLUMN "public"."gen_table_column"."ispk" IS '是否主键（1是）';
COMMENT ON COLUMN "public"."gen_table_column"."isincrement" IS '是否自增（1是）';
COMMENT ON COLUMN "public"."gen_table_column"."isrequired" IS '是否必填（1是）';
COMMENT ON COLUMN "public"."gen_table_column"."isinsert" IS '是否为插入字段（1是）';
COMMENT ON COLUMN "public"."gen_table_column"."isedit" IS '是否编辑字段（1是）';
COMMENT ON COLUMN "public"."gen_table_column"."islist" IS '是否列表字段（1是）';
COMMENT ON COLUMN "public"."gen_table_column"."isquery" IS '是否查询字段（1是）';
COMMENT ON COLUMN "public"."gen_table_column"."issort" IS '是否排序字段（1是）';
COMMENT ON COLUMN "public"."gen_table_column"."isexport" IS '是否导出字段（1是）';
COMMENT ON COLUMN "public"."gen_table_column"."querytype" IS '查询方式（等于、不等于、大于、小于、范围）';
COMMENT ON COLUMN "public"."gen_table_column"."htmltype" IS '显示类型（文本框、文本域、下拉框、复选框、单选框、日期控件）';
COMMENT ON COLUMN "public"."gen_table_column"."dicttype" IS '字典类型';
COMMENT ON COLUMN "public"."gen_table_column"."sort" IS '排序';
COMMENT ON COLUMN "public"."gen_table_column"."create_by" IS '创建者';
COMMENT ON COLUMN "public"."gen_table_column"."create_time" IS '创建时间';
COMMENT ON COLUMN "public"."gen_table_column"."update_by" IS '更新者';
COMMENT ON COLUMN "public"."gen_table_column"."update_time" IS '更新时间';
COMMENT ON TABLE "public"."gen_table_column" IS '代码生成业务表字段';

-- ----------------------------
-- Records of gen_table_column
-- ----------------------------

-- ----------------------------
-- Table structure for sys_common_lang
-- ----------------------------
DROP TABLE IF EXISTS "public"."sys_common_lang";
CREATE TABLE "public"."sys_common_lang" (
  "id" int8 NOT NULL,
  "lang_code" varchar(10) COLLATE "pg_catalog"."default" NOT NULL,
  "lang_key" varchar(100) COLLATE "pg_catalog"."default" NOT NULL,
  "lang_name" varchar(2000) COLLATE "pg_catalog"."default" NOT NULL,
  "addtime" timestamp(6)
)
;
COMMENT ON COLUMN "public"."sys_common_lang"."id" IS 'id';
COMMENT ON COLUMN "public"."sys_common_lang"."lang_code" IS '语言code eg：zh-cn';
COMMENT ON COLUMN "public"."sys_common_lang"."lang_key" IS '翻译key值';
COMMENT ON COLUMN "public"."sys_common_lang"."lang_name" IS '翻译内容';
COMMENT ON COLUMN "public"."sys_common_lang"."addtime" IS '添加时间';

-- ----------------------------
-- Records of sys_common_lang
-- ----------------------------

-- ----------------------------
-- Table structure for sys_config
-- ----------------------------
DROP TABLE IF EXISTS "public"."sys_config";
CREATE TABLE "public"."sys_config" (
  "configid" int4 NOT NULL DEFAULT nextval('sys_configid_seq'::regclass),
  "configname" varchar(100) COLLATE "pg_catalog"."default",
  "configkey" varchar(100) COLLATE "pg_catalog"."default",
  "configvalue" varchar(500) COLLATE "pg_catalog"."default",
  "configtype" char(1) COLLATE "pg_catalog"."default",
  "create_by" varchar(64) COLLATE "pg_catalog"."default",
  "create_time" timestamp(6),
  "update_by" varchar(64) COLLATE "pg_catalog"."default",
  "update_time" timestamp(6),
  "remark" varchar(500) COLLATE "pg_catalog"."default"
)
;
COMMENT ON COLUMN "public"."sys_config"."configid" IS '参数主键';
COMMENT ON COLUMN "public"."sys_config"."configname" IS '参数名称';
COMMENT ON COLUMN "public"."sys_config"."configkey" IS '参数键名';
COMMENT ON COLUMN "public"."sys_config"."configvalue" IS '参数键值';
COMMENT ON COLUMN "public"."sys_config"."configtype" IS '系统内置（Y是 N否）';
COMMENT ON COLUMN "public"."sys_config"."create_by" IS '创建者';
COMMENT ON COLUMN "public"."sys_config"."create_time" IS '创建时间';
COMMENT ON COLUMN "public"."sys_config"."update_by" IS '更新者';
COMMENT ON COLUMN "public"."sys_config"."update_time" IS '更新时间';
COMMENT ON COLUMN "public"."sys_config"."remark" IS '备注';
COMMENT ON TABLE "public"."sys_config" IS '参数配置表';


-- ----------------------------
-- Table structure for sys_dept
-- ----------------------------
DROP TABLE IF EXISTS "public"."sys_dept";
CREATE TABLE "public"."sys_dept" (
  "deptid" int8 NOT NULL DEFAULT nextval('sys_deptid_seq'::regclass),
  "parentid" int8,
  "ancestors" varchar(50) COLLATE "pg_catalog"."default",
  "deptname" varchar(30) COLLATE "pg_catalog"."default",
  "ordernum" int4,
  "leader" varchar(20) COLLATE "pg_catalog"."default",
  "phone" varchar(11) COLLATE "pg_catalog"."default",
  "email" varchar(50) COLLATE "pg_catalog"."default",
  "status" char(1) COLLATE "pg_catalog"."default",
  "delflag" char(1) COLLATE "pg_catalog"."default",
  "create_by" varchar(64) COLLATE "pg_catalog"."default",
  "create_time" timestamp(6),
  "update_by" varchar(64) COLLATE "pg_catalog"."default",
  "update_time" timestamp(6),
  "remark" varchar(255) COLLATE "pg_catalog"."default"
)
;
COMMENT ON COLUMN "public"."sys_dept"."deptid" IS '部门id';
COMMENT ON COLUMN "public"."sys_dept"."parentid" IS '父部门id';
COMMENT ON COLUMN "public"."sys_dept"."ancestors" IS '祖级列表';
COMMENT ON COLUMN "public"."sys_dept"."deptname" IS '部门名称';
COMMENT ON COLUMN "public"."sys_dept"."ordernum" IS '显示顺序';
COMMENT ON COLUMN "public"."sys_dept"."leader" IS '负责人';
COMMENT ON COLUMN "public"."sys_dept"."phone" IS '联系电话';
COMMENT ON COLUMN "public"."sys_dept"."email" IS '邮箱';
COMMENT ON COLUMN "public"."sys_dept"."status" IS '部门状态（0正常 1停用）';
COMMENT ON COLUMN "public"."sys_dept"."delflag" IS '删除标志（0代表存在 2代表删除）';
COMMENT ON COLUMN "public"."sys_dept"."create_by" IS '创建者';
COMMENT ON COLUMN "public"."sys_dept"."create_time" IS '创建时间';
COMMENT ON COLUMN "public"."sys_dept"."update_by" IS '更新者';
COMMENT ON COLUMN "public"."sys_dept"."update_time" IS '更新时间';
COMMENT ON COLUMN "public"."sys_dept"."remark" IS '备注';
COMMENT ON TABLE "public"."sys_dept" IS '部门表';

-- ----------------------------
-- Table structure for sys_dict_data
-- ----------------------------
DROP TABLE IF EXISTS "public"."sys_dict_data";
CREATE TABLE "public"."sys_dict_data" (
  "dictcode" int8 NOT NULL DEFAULT nextval('sys_dict_dataid_seq'::regclass),
  "dictsort" int4,
  "dictlabel" varchar(100) COLLATE "pg_catalog"."default",
  "dictvalue" varchar(100) COLLATE "pg_catalog"."default",
  "dicttype" varchar(100) COLLATE "pg_catalog"."default",
  "cssclass" varchar(100) COLLATE "pg_catalog"."default",
  "listclass" varchar(100) COLLATE "pg_catalog"."default",
  "isdefault" char(1) COLLATE "pg_catalog"."default",
  "status" char(1) COLLATE "pg_catalog"."default",
  "create_by" varchar(64) COLLATE "pg_catalog"."default",
  "create_time" timestamp(6),
  "update_by" varchar(64) COLLATE "pg_catalog"."default",
  "update_time" timestamp(6),
  "remark" varchar(500) COLLATE "pg_catalog"."default"
)
;
COMMENT ON COLUMN "public"."sys_dict_data"."dictcode" IS '字典编码';
COMMENT ON COLUMN "public"."sys_dict_data"."dictsort" IS '字典排序';
COMMENT ON COLUMN "public"."sys_dict_data"."dictlabel" IS '字典标签';
COMMENT ON COLUMN "public"."sys_dict_data"."dictvalue" IS '字典键值';
COMMENT ON COLUMN "public"."sys_dict_data"."dicttype" IS '字典类型';
COMMENT ON COLUMN "public"."sys_dict_data"."cssclass" IS '样式属性（其他样式扩展）';
COMMENT ON COLUMN "public"."sys_dict_data"."listclass" IS '表格回显样式';
COMMENT ON COLUMN "public"."sys_dict_data"."isdefault" IS '是否默认（Y是 N否）';
COMMENT ON COLUMN "public"."sys_dict_data"."status" IS '状态（0正常 1停用）';
COMMENT ON COLUMN "public"."sys_dict_data"."create_by" IS '创建者';
COMMENT ON COLUMN "public"."sys_dict_data"."create_time" IS '创建时间';
COMMENT ON COLUMN "public"."sys_dict_data"."update_by" IS '更新者';
COMMENT ON COLUMN "public"."sys_dict_data"."update_time" IS '更新时间';
COMMENT ON COLUMN "public"."sys_dict_data"."remark" IS '备注';
COMMENT ON TABLE "public"."sys_dict_data" IS '字典数据表';

-- ----------------------------
-- Table structure for sys_dict_type
-- ----------------------------
DROP TABLE IF EXISTS "public"."sys_dict_type";
CREATE TABLE "public"."sys_dict_type" (
  "dictid" int8 NOT NULL DEFAULT nextval('sys_dict_typeid_seq'::regclass),
  "dictname" varchar(100) COLLATE "pg_catalog"."default",
  "dicttype" varchar(100) COLLATE "pg_catalog"."default",
  "status" char(1) COLLATE "pg_catalog"."default",
  "type" char(1) COLLATE "pg_catalog"."default",
  "create_by" varchar(64) COLLATE "pg_catalog"."default",
  "create_time" timestamp(6),
  "update_by" varchar(64) COLLATE "pg_catalog"."default",
  "update_time" timestamp(6),
  "remark" varchar(500) COLLATE "pg_catalog"."default",
  "customsql" varchar(500) COLLATE "pg_catalog"."default"
)
;
COMMENT ON COLUMN "public"."sys_dict_type"."dictid" IS '字典主键';
COMMENT ON COLUMN "public"."sys_dict_type"."dictname" IS '字典名称';
COMMENT ON COLUMN "public"."sys_dict_type"."dicttype" IS '字典类型';
COMMENT ON COLUMN "public"."sys_dict_type"."status" IS '状态（0正常 1停用）';
COMMENT ON COLUMN "public"."sys_dict_type"."type" IS '系统内置（Y是 N否）';
COMMENT ON COLUMN "public"."sys_dict_type"."create_by" IS '创建者';
COMMENT ON COLUMN "public"."sys_dict_type"."create_time" IS '创建时间';
COMMENT ON COLUMN "public"."sys_dict_type"."update_by" IS '更新者';
COMMENT ON COLUMN "public"."sys_dict_type"."update_time" IS '更新时间';
COMMENT ON COLUMN "public"."sys_dict_type"."remark" IS '备注';
COMMENT ON COLUMN "public"."sys_dict_type"."customsql" IS '自定义sql语句';
COMMENT ON TABLE "public"."sys_dict_type" IS '字典类型表';

-- ----------------------------
-- Table structure for sys_file
-- ----------------------------
DROP TABLE IF EXISTS "public"."sys_file";
CREATE TABLE "public"."sys_file" (
  "id" int8 NOT NULL,
  "realname" varchar(50) COLLATE "pg_catalog"."default",
  "filename" varchar(50) COLLATE "pg_catalog"."default",
  "fileurl" varchar(500) COLLATE "pg_catalog"."default",
  "storepath" varchar(50) COLLATE "pg_catalog"."default",
  "accessurl" varchar(300) COLLATE "pg_catalog"."default",
  "filesize" varchar(20) COLLATE "pg_catalog"."default",
  "filetype" varchar(200) COLLATE "pg_catalog"."default",
  "fileext" varchar(10) COLLATE "pg_catalog"."default",
  "create_by" varchar(50) COLLATE "pg_catalog"."default",
  "create_time" timestamp(6),
  "storetype" int4
)
;
COMMENT ON COLUMN "public"."sys_file"."realname" IS '文件真实名';
COMMENT ON COLUMN "public"."sys_file"."filename" IS '文件名';
COMMENT ON COLUMN "public"."sys_file"."fileurl" IS '文件存储地址';
COMMENT ON COLUMN "public"."sys_file"."storepath" IS '仓库位置';
COMMENT ON COLUMN "public"."sys_file"."accessurl" IS '访问路径';
COMMENT ON COLUMN "public"."sys_file"."filesize" IS '文件大小';
COMMENT ON COLUMN "public"."sys_file"."filetype" IS '文件类型';
COMMENT ON COLUMN "public"."sys_file"."fileext" IS '文件扩展名';
COMMENT ON COLUMN "public"."sys_file"."create_by" IS '创建人';
COMMENT ON COLUMN "public"."sys_file"."create_time" IS '上传时间';
COMMENT ON COLUMN "public"."sys_file"."storetype" IS '存储类型';

-- ----------------------------
-- Table structure for sys_logininfor
-- ----------------------------
DROP TABLE IF EXISTS "public"."sys_logininfor";
CREATE TABLE "public"."sys_logininfor" (
  "infoid" int8 NOT NULL DEFAULT nextval('sys_logininforid_seq'::regclass),
  "username" varchar(50) COLLATE "pg_catalog"."default",
  "ipaddr" varchar(50) COLLATE "pg_catalog"."default",
  "loginlocation" varchar(255) COLLATE "pg_catalog"."default",
  "browser" varchar(50) COLLATE "pg_catalog"."default",
  "os" varchar(50) COLLATE "pg_catalog"."default",
  "status" char(1) COLLATE "pg_catalog"."default",
  "msg" varchar(255) COLLATE "pg_catalog"."default",
  "logintime" timestamp(6)
)
;
COMMENT ON COLUMN "public"."sys_logininfor"."infoid" IS '访问ID';
COMMENT ON COLUMN "public"."sys_logininfor"."username" IS '用户账号';
COMMENT ON COLUMN "public"."sys_logininfor"."ipaddr" IS '登录IP地址';
COMMENT ON COLUMN "public"."sys_logininfor"."loginlocation" IS '登录地点';
COMMENT ON COLUMN "public"."sys_logininfor"."browser" IS '浏览器类型';
COMMENT ON COLUMN "public"."sys_logininfor"."os" IS '操作系统';
COMMENT ON COLUMN "public"."sys_logininfor"."status" IS '登录状态（0成功 1失败）';
COMMENT ON COLUMN "public"."sys_logininfor"."msg" IS '提示消息';
COMMENT ON COLUMN "public"."sys_logininfor"."logintime" IS '访问时间';
COMMENT ON TABLE "public"."sys_logininfor" IS '系统访问记录';

-- ----------------------------
-- Table structure for sys_menu
-- ----------------------------
DROP TABLE IF EXISTS "public"."sys_menu";
CREATE TABLE "public"."sys_menu" (
  "menuid" int8 NOT NULL DEFAULT nextval('sys_menuid_seq'::regclass),
  "menuname" varchar(50) COLLATE "pg_catalog"."default" NOT NULL,
  "parentid" int8,
  "ordernum" int4,
  "path" varchar(200) COLLATE "pg_catalog"."default",
  "component" varchar(255) COLLATE "pg_catalog"."default",
  "isframe" int4,
  "iscache" int4,
  "menutype" char(1) COLLATE "pg_catalog"."default",
  "visible" char(1) COLLATE "pg_catalog"."default",
  "status" char(1) COLLATE "pg_catalog"."default",
  "perms" varchar(100) COLLATE "pg_catalog"."default",
  "icon" varchar(100) COLLATE "pg_catalog"."default",
  "create_by" varchar(64) COLLATE "pg_catalog"."default",
  "create_time" timestamp(6),
  "update_by" varchar(64) COLLATE "pg_catalog"."default",
  "update_time" timestamp(6),
  "remark" varchar(500) COLLATE "pg_catalog"."default",
  "menuname_key" varchar(100) COLLATE "pg_catalog"."default"
)
;
COMMENT ON COLUMN "public"."sys_menu"."menuid" IS '菜单ID';
COMMENT ON COLUMN "public"."sys_menu"."menuname" IS '菜单名称';
COMMENT ON COLUMN "public"."sys_menu"."parentid" IS '父菜单ID';
COMMENT ON COLUMN "public"."sys_menu"."ordernum" IS '显示顺序';
COMMENT ON COLUMN "public"."sys_menu"."path" IS '路由地址';
COMMENT ON COLUMN "public"."sys_menu"."component" IS '组件路径';
COMMENT ON COLUMN "public"."sys_menu"."isframe" IS '是否外链(0 否 1 是)';
COMMENT ON COLUMN "public"."sys_menu"."iscache" IS '是否缓存(0缓存 1不缓存)';
COMMENT ON COLUMN "public"."sys_menu"."menutype" IS '菜单类型（M目录 C菜单 F按钮 L链接）';
COMMENT ON COLUMN "public"."sys_menu"."visible" IS '菜单状态（0显示 1隐藏）';
COMMENT ON COLUMN "public"."sys_menu"."status" IS '菜单状态（0正常 1停用）';
COMMENT ON COLUMN "public"."sys_menu"."perms" IS '权限标识';
COMMENT ON COLUMN "public"."sys_menu"."icon" IS '菜单图标';
COMMENT ON COLUMN "public"."sys_menu"."create_by" IS '创建者';
COMMENT ON COLUMN "public"."sys_menu"."create_time" IS '创建时间';
COMMENT ON COLUMN "public"."sys_menu"."update_by" IS '更新者';
COMMENT ON COLUMN "public"."sys_menu"."update_time" IS '更新时间';
COMMENT ON COLUMN "public"."sys_menu"."remark" IS '备注';
COMMENT ON COLUMN "public"."sys_menu"."menuname_key" IS '菜单名翻译字典名';
COMMENT ON TABLE "public"."sys_menu" IS '菜单权限表';


-- ----------------------------
-- Table structure for sys_notice
-- ----------------------------
DROP TABLE IF EXISTS "public"."sys_notice";
CREATE TABLE "public"."sys_notice" (
  "notice_id" int4 NOT NULL DEFAULT nextval('sys_noticeid_seq'::regclass),
  "notice_title" varchar(50) COLLATE "pg_catalog"."default" NOT NULL,
  "notice_type" char(1) COLLATE "pg_catalog"."default" NOT NULL,
  "notice_content" varchar(500) COLLATE "pg_catalog"."default",
  "status" char(1) COLLATE "pg_catalog"."default",
  "create_by" varchar(64) COLLATE "pg_catalog"."default",
  "create_time" timestamp(6),
  "update_by" varchar(64) COLLATE "pg_catalog"."default",
  "update_time" timestamp(6),
  "remark" varchar(255) COLLATE "pg_catalog"."default"
)
;
COMMENT ON COLUMN "public"."sys_notice"."notice_id" IS '公告ID';
COMMENT ON COLUMN "public"."sys_notice"."notice_title" IS '公告标题';
COMMENT ON COLUMN "public"."sys_notice"."notice_type" IS '公告类型（1通知 2公告）';
COMMENT ON COLUMN "public"."sys_notice"."notice_content" IS '公告内容';
COMMENT ON COLUMN "public"."sys_notice"."status" IS '公告状态（0正常 1关闭）';
COMMENT ON COLUMN "public"."sys_notice"."create_by" IS '创建者';
COMMENT ON COLUMN "public"."sys_notice"."create_time" IS '创建时间';
COMMENT ON COLUMN "public"."sys_notice"."update_by" IS '更新者';
COMMENT ON COLUMN "public"."sys_notice"."update_time" IS '更新时间';
COMMENT ON COLUMN "public"."sys_notice"."remark" IS '备注';
COMMENT ON TABLE "public"."sys_notice" IS '通知公告表';

-- ----------------------------
-- Table structure for sys_oper_log
-- ----------------------------
DROP TABLE IF EXISTS "public"."sys_oper_log";
CREATE TABLE "public"."sys_oper_log" (
  "operid" int8 NOT NULL DEFAULT nextval('sys_oper_logid_seq'::regclass),
  "title" varchar(50) COLLATE "pg_catalog"."default",
  "businesstype" int4,
  "method" varchar(100) COLLATE "pg_catalog"."default",
  "requestmethod" varchar(10) COLLATE "pg_catalog"."default",
  "operatortype" int4,
  "opername" varchar(50) COLLATE "pg_catalog"."default",
  "deptname" varchar(50) COLLATE "pg_catalog"."default",
  "operurl" varchar(255) COLLATE "pg_catalog"."default",
  "operip" varchar(50) COLLATE "pg_catalog"."default",
  "operlocation" varchar(255) COLLATE "pg_catalog"."default",
  "operparam" varchar(2000) COLLATE "pg_catalog"."default",
  "jsonresult" varchar(5000) COLLATE "pg_catalog"."default",
  "status" int4,
  "errormsg" varchar(2000) COLLATE "pg_catalog"."default",
  "opertime" timestamp(6),
  "elapsed" int8
)
;
COMMENT ON COLUMN "public"."sys_oper_log"."operid" IS '日志主键';
COMMENT ON COLUMN "public"."sys_oper_log"."title" IS '模块标题';
COMMENT ON COLUMN "public"."sys_oper_log"."businesstype" IS '业务类型（0其它 1新增 2修改 3删除）';
COMMENT ON COLUMN "public"."sys_oper_log"."method" IS '方法名称';
COMMENT ON COLUMN "public"."sys_oper_log"."requestmethod" IS '请求方式';
COMMENT ON COLUMN "public"."sys_oper_log"."operatortype" IS '操作类别（0其它 1后台用户 2手机端用户）';
COMMENT ON COLUMN "public"."sys_oper_log"."opername" IS '操作人员';
COMMENT ON COLUMN "public"."sys_oper_log"."deptname" IS '部门名称';
COMMENT ON COLUMN "public"."sys_oper_log"."operurl" IS '请求URL';
COMMENT ON COLUMN "public"."sys_oper_log"."operip" IS '主机地址';
COMMENT ON COLUMN "public"."sys_oper_log"."operlocation" IS '操作地点';
COMMENT ON COLUMN "public"."sys_oper_log"."operparam" IS '请求参数';
COMMENT ON COLUMN "public"."sys_oper_log"."jsonresult" IS '返回参数';
COMMENT ON COLUMN "public"."sys_oper_log"."status" IS '操作状态（0正常 1异常）';
COMMENT ON COLUMN "public"."sys_oper_log"."errormsg" IS '错误消息';
COMMENT ON COLUMN "public"."sys_oper_log"."opertime" IS '操作时间';
COMMENT ON COLUMN "public"."sys_oper_log"."elapsed" IS '请求用时';
COMMENT ON TABLE "public"."sys_oper_log" IS '操作日志记录';


-- ----------------------------
-- Table structure for sys_post
-- ----------------------------
DROP TABLE IF EXISTS "public"."sys_post";
CREATE TABLE "public"."sys_post" (
  "postid" int8 NOT NULL DEFAULT nextval('sys_postid_seq'::regclass),
  "postcode" varchar(64) COLLATE "pg_catalog"."default" NOT NULL,
  "postname" varchar(50) COLLATE "pg_catalog"."default" NOT NULL,
  "postsort" int4 NOT NULL,
  "status" char(1) COLLATE "pg_catalog"."default" NOT NULL,
  "create_by" varchar(64) COLLATE "pg_catalog"."default",
  "create_time" timestamp(6),
  "update_by" varchar(64) COLLATE "pg_catalog"."default",
  "update_time" timestamp(6),
  "remark" varchar(500) COLLATE "pg_catalog"."default"
)
;
COMMENT ON COLUMN "public"."sys_post"."postid" IS '岗位ID';
COMMENT ON COLUMN "public"."sys_post"."postcode" IS '岗位编码';
COMMENT ON COLUMN "public"."sys_post"."postname" IS '岗位名称';
COMMENT ON COLUMN "public"."sys_post"."postsort" IS '显示顺序';
COMMENT ON COLUMN "public"."sys_post"."status" IS '状态（0正常 1停用）';
COMMENT ON COLUMN "public"."sys_post"."create_by" IS '创建者';
COMMENT ON COLUMN "public"."sys_post"."create_time" IS '创建时间';
COMMENT ON COLUMN "public"."sys_post"."update_by" IS '更新者';
COMMENT ON COLUMN "public"."sys_post"."update_time" IS '更新时间';
COMMENT ON COLUMN "public"."sys_post"."remark" IS '备注';
COMMENT ON TABLE "public"."sys_post" IS '岗位信息表';

-- ----------------------------
-- Table structure for sys_role
-- ----------------------------
DROP TABLE IF EXISTS "public"."sys_role";
CREATE TABLE "public"."sys_role" (
  "roleid" int8 NOT NULL DEFAULT nextval('sys_roleid_seq'::regclass),
  "rolename" varchar(30) COLLATE "pg_catalog"."default" NOT NULL,
  "rolekey" varchar(100) COLLATE "pg_catalog"."default" NOT NULL,
  "rolesort" int4 NOT NULL,
  "datascope" int4 COLLATE "pg_catalog"."default",
  "menu_check_strictly" int2,
  "dept_check_strictly" int2 NOT NULL,
  "status" int4 COLLATE "pg_catalog"."default" NOT NULL,
  "delflag" int4 COLLATE "pg_catalog"."default" NOT NULL,
  "create_by" varchar(64) COLLATE "pg_catalog"."default",
  "create_time" timestamp(6),
  "update_by" varchar(64) COLLATE "pg_catalog"."default",
  "update_time" timestamp(6),
  "remark" varchar(500) COLLATE "pg_catalog"."default"
)
;
COMMENT ON COLUMN "public"."sys_role"."roleid" IS '角色ID';
COMMENT ON COLUMN "public"."sys_role"."rolename" IS '角色名称';
COMMENT ON COLUMN "public"."sys_role"."rolekey" IS '角色权限字符串';
COMMENT ON COLUMN "public"."sys_role"."rolesort" IS '显示顺序';
COMMENT ON COLUMN "public"."sys_role"."datascope" IS '数据范围（1：全部数据权限 2：自定数据权限 3：本部门数据权限 ）';
COMMENT ON COLUMN "public"."sys_role"."menu_check_strictly" IS '菜单树选择项是否关联显示';
COMMENT ON COLUMN "public"."sys_role"."dept_check_strictly" IS '部门树选择项是否关联显示';
COMMENT ON COLUMN "public"."sys_role"."status" IS '角色状态（0正常 1停用）';
COMMENT ON COLUMN "public"."sys_role"."delflag" IS '删除标志（0代表存在 2代表删除）';
COMMENT ON COLUMN "public"."sys_role"."create_by" IS '创建者';
COMMENT ON COLUMN "public"."sys_role"."create_time" IS '创建时间';
COMMENT ON COLUMN "public"."sys_role"."update_by" IS '更新者';
COMMENT ON COLUMN "public"."sys_role"."update_time" IS '更新时间';
COMMENT ON COLUMN "public"."sys_role"."remark" IS '备注';
COMMENT ON TABLE "public"."sys_role" IS '角色信息表';

-- ----------------------------
-- Table structure for sys_role_dept
-- ----------------------------
DROP TABLE IF EXISTS "public"."sys_role_dept";
CREATE TABLE "public"."sys_role_dept" (
  "roleid" int8 NOT NULL,
  "deptid" int8 NOT NULL
)
;
COMMENT ON COLUMN "public"."sys_role_dept"."roleid" IS '角色ID';
COMMENT ON COLUMN "public"."sys_role_dept"."deptid" IS '部门ID';
COMMENT ON TABLE "public"."sys_role_dept" IS '角色和部门关联表';

-- ----------------------------
-- Table structure for sys_role_menu
-- ----------------------------
DROP TABLE IF EXISTS "public"."sys_role_menu";
CREATE TABLE "public"."sys_role_menu" (
  "role_id" int8 NOT NULL,
  "menu_id" int8 NOT NULL,
  "create_by" varchar(20) COLLATE "pg_catalog"."default",
  "create_time" timestamp(6)
)
;
COMMENT ON COLUMN "public"."sys_role_menu"."role_id" IS '角色ID';
COMMENT ON COLUMN "public"."sys_role_menu"."menu_id" IS '菜单ID';
COMMENT ON TABLE "public"."sys_role_menu" IS '角色和菜单关联表';

-- ----------------------------
-- Table structure for sys_tasks
-- ----------------------------
DROP TABLE IF EXISTS "public"."sys_tasks";
CREATE TABLE "public"."sys_tasks" (
  "id" varchar(20) COLLATE "pg_catalog"."default" NOT NULL,
  "name" varchar(255) COLLATE "pg_catalog"."default" NOT NULL,
  "jobgroup" varchar(255) COLLATE "pg_catalog"."default" NOT NULL,
  "cron" varchar(255) COLLATE "pg_catalog"."default",
  "assemblyname" varchar(255) COLLATE "pg_catalog"."default" NOT NULL,
  "classname" varchar(255) COLLATE "pg_catalog"."default" NOT NULL,
  "remark" text COLLATE "pg_catalog"."default",
  "runtimes" int4 NOT NULL,
  "begintime" timestamp(6),
  "endtime" timestamp(6),
  "triggertype" int4 NOT NULL,
  "intervalsecond" int4 NOT NULL,
  "isstart" int2 NOT NULL,
  "jobparams" text COLLATE "pg_catalog"."default",
  "create_time" timestamp(6),
  "update_time" timestamp(6),
  "create_by" varchar(50) COLLATE "pg_catalog"."default",
  "update_by" varchar(50) COLLATE "pg_catalog"."default",
  "lastruntime" timestamp(6),
  "apiurl" varchar(255) COLLATE "pg_catalog"."default",
  "tasktype" int4,
  "sqltext" varchar(1000) COLLATE "pg_catalog"."default" NOT NULL,
  "requestmethod" varchar(20) COLLATE "pg_catalog"."default" NOT NULL
)
;
COMMENT ON COLUMN "public"."sys_tasks"."id" IS 'UID';
COMMENT ON COLUMN "public"."sys_tasks"."name" IS '任务名称';
COMMENT ON COLUMN "public"."sys_tasks"."jobgroup" IS '任务分组';
COMMENT ON COLUMN "public"."sys_tasks"."cron" IS '运行时间表达式';
COMMENT ON COLUMN "public"."sys_tasks"."assemblyname" IS '程序集名称';
COMMENT ON COLUMN "public"."sys_tasks"."classname" IS '任务所在类';
COMMENT ON COLUMN "public"."sys_tasks"."remark" IS '任务描述';
COMMENT ON COLUMN "public"."sys_tasks"."runtimes" IS '执行次数';
COMMENT ON COLUMN "public"."sys_tasks"."begintime" IS '开始时间';
COMMENT ON COLUMN "public"."sys_tasks"."endtime" IS '结束时间';
COMMENT ON COLUMN "public"."sys_tasks"."triggertype" IS '触发器类型（0、simple 1、cron）';
COMMENT ON COLUMN "public"."sys_tasks"."intervalsecond" IS '执行间隔时间(单位:秒)';
COMMENT ON COLUMN "public"."sys_tasks"."isstart" IS '是否启动';
COMMENT ON COLUMN "public"."sys_tasks"."jobparams" IS '传入参数';
COMMENT ON COLUMN "public"."sys_tasks"."create_time" IS '创建时间';
COMMENT ON COLUMN "public"."sys_tasks"."update_time" IS '最后更新时间';
COMMENT ON COLUMN "public"."sys_tasks"."create_by" IS '创建人编码';
COMMENT ON COLUMN "public"."sys_tasks"."update_by" IS '更新人编码';
COMMENT ON COLUMN "public"."sys_tasks"."lastruntime" IS '最后执行时间';
COMMENT ON COLUMN "public"."sys_tasks"."apiurl" IS 'api执行地址';
COMMENT ON COLUMN "public"."sys_tasks"."tasktype" IS '任务类型1程序集任务 2网络请求';
COMMENT ON COLUMN "public"."sys_tasks"."sqltext" IS 'SQL语句';
COMMENT ON COLUMN "public"."sys_tasks"."requestmethod" IS 'http请求方法';
COMMENT ON TABLE "public"."sys_tasks" IS '计划任务';


-- ----------------------------
-- Table structure for sys_tasks_log
-- ----------------------------
DROP TABLE IF EXISTS "public"."sys_tasks_log";
CREATE TABLE "public"."sys_tasks_log" (
  "joblogid" int8 NOT NULL DEFAULT nextval('sys_tasks_logid_seq'::regclass),
  "jobid" varchar(20) COLLATE "pg_catalog"."default" NOT NULL,
  "jobname" varchar(64) COLLATE "pg_catalog"."default" NOT NULL,
  "jobgroup" varchar(64) COLLATE "pg_catalog"."default" NOT NULL,
  "jobmessage" varchar(500) COLLATE "pg_catalog"."default",
  "status" char(1) COLLATE "pg_catalog"."default",
  "exception" varchar(2000) COLLATE "pg_catalog"."default",
  "createtime" timestamp(6),
  "invoketarget" varchar(200) COLLATE "pg_catalog"."default",
  "elapsed" float8
)
;
COMMENT ON COLUMN "public"."sys_tasks_log"."joblogid" IS '任务日志ID';
COMMENT ON COLUMN "public"."sys_tasks_log"."jobid" IS '任务id';
COMMENT ON COLUMN "public"."sys_tasks_log"."jobname" IS '任务名称';
COMMENT ON COLUMN "public"."sys_tasks_log"."jobgroup" IS '任务组名';
COMMENT ON COLUMN "public"."sys_tasks_log"."jobmessage" IS '日志信息';
COMMENT ON COLUMN "public"."sys_tasks_log"."status" IS '执行状态（0正常 1失败）';
COMMENT ON COLUMN "public"."sys_tasks_log"."exception" IS '异常信息';
COMMENT ON COLUMN "public"."sys_tasks_log"."createtime" IS '创建时间';
COMMENT ON COLUMN "public"."sys_tasks_log"."invoketarget" IS '调用目标';
COMMENT ON COLUMN "public"."sys_tasks_log"."elapsed" IS '作业用时';
COMMENT ON TABLE "public"."sys_tasks_log" IS '定时任务调度日志表';

-- ----------------------------
-- Table structure for sys_user
-- ----------------------------
DROP TABLE IF EXISTS "public"."sys_user";
CREATE TABLE "public"."sys_user" (
  "userid" int8 NOT NULL DEFAULT nextval('sys_userid_seq'::regclass),
  "deptid" int8,
  "username" varchar(30) COLLATE "pg_catalog"."default" NOT NULL,
  "nickname" varchar(30) COLLATE "pg_catalog"."default" NOT NULL,
  "usertype" int4 COLLATE "pg_catalog"."default",
  "email" varchar(50) COLLATE "pg_catalog"."default",
  "phonenumber" varchar(11) COLLATE "pg_catalog"."default",
  "sex" int4 COLLATE "pg_catalog"."default",
  "avatar" varchar(400) COLLATE "pg_catalog"."default",
  "password" varchar(100) COLLATE "pg_catalog"."default",
  "status" int4 COLLATE "pg_catalog"."default",
  "delflag" int4 COLLATE "pg_catalog"."default",
  "loginip" varchar(50) COLLATE "pg_catalog"."default",
  "logindate" timestamp(6),
  "create_by" varchar(64) COLLATE "pg_catalog"."default",
  "create_time" timestamp(6),
  "update_by" varchar(64) COLLATE "pg_catalog"."default",
  "update_time" timestamp(6),
  "remark" varchar(500) COLLATE "pg_catalog"."default"
)
;
COMMENT ON COLUMN "public"."sys_user"."userid" IS '用户ID';
COMMENT ON COLUMN "public"."sys_user"."deptid" IS '部门ID';
COMMENT ON COLUMN "public"."sys_user"."username" IS '用户账号';
COMMENT ON COLUMN "public"."sys_user"."nickname" IS '用户昵称';
COMMENT ON COLUMN "public"."sys_user"."usertype" IS '用户类型（00系统用户）';
COMMENT ON COLUMN "public"."sys_user"."email" IS '用户邮箱';
COMMENT ON COLUMN "public"."sys_user"."phonenumber" IS '手机号码';
COMMENT ON COLUMN "public"."sys_user"."sex" IS '用户性别（0男 1女 2未知）';
COMMENT ON COLUMN "public"."sys_user"."avatar" IS '头像地址';
COMMENT ON COLUMN "public"."sys_user"."password" IS '密码';
COMMENT ON COLUMN "public"."sys_user"."status" IS '帐号状态（0正常 1停用）';
COMMENT ON COLUMN "public"."sys_user"."delflag" IS '删除标志（0代表存在 2代表删除）';
COMMENT ON COLUMN "public"."sys_user"."loginip" IS '最后登录IP';
COMMENT ON COLUMN "public"."sys_user"."logindate" IS '最后登录时间';
COMMENT ON COLUMN "public"."sys_user"."create_by" IS '创建者';
COMMENT ON COLUMN "public"."sys_user"."create_time" IS '创建时间';
COMMENT ON COLUMN "public"."sys_user"."update_by" IS '更新者';
COMMENT ON COLUMN "public"."sys_user"."update_time" IS '更新时间';
COMMENT ON COLUMN "public"."sys_user"."remark" IS '备注';
COMMENT ON TABLE "public"."sys_user" IS '用户信息表';

-- ----------------------------
-- Table structure for sys_user_post
-- ----------------------------
DROP TABLE IF EXISTS "public"."sys_user_post";
CREATE TABLE "public"."sys_user_post" (
  "userid" int8 NOT NULL,
  "postid" int8 NOT NULL
)
;
COMMENT ON COLUMN "public"."sys_user_post"."userid" IS '用户ID';
COMMENT ON COLUMN "public"."sys_user_post"."postid" IS '岗位ID';
COMMENT ON TABLE "public"."sys_user_post" IS '用户与岗位关联表';


-- ----------------------------
-- Table structure for sys_user_role
-- ----------------------------
DROP TABLE IF EXISTS "public"."sys_user_role";
CREATE TABLE "public"."sys_user_role" (
  "user_id" int8 NOT NULL,
  "role_id" int8 NOT NULL
)
;
COMMENT ON COLUMN "public"."sys_user_role"."user_id" IS '用户ID';
COMMENT ON COLUMN "public"."sys_user_role"."role_id" IS '角色ID';
COMMENT ON TABLE "public"."sys_user_role" IS '用户和角色关联表';


-- ----------------------------
-- Function structure for exec
-- ----------------------------
DROP FUNCTION IF EXISTS "public"."exec"("sqlstring" varchar);
CREATE OR REPLACE FUNCTION "public"."exec"("sqlstring" varchar)
  RETURNS "pg_catalog"."varchar" AS $BODY$
    declare
        res varchar(50);
    BEGIN
        EXECUTE sqlstring;
        RETURN 'ok';
    END
$BODY$
  LANGUAGE plpgsql VOLATILE
  COST 100;

-- ----------------------------
-- Alter sequences owned by
-- ----------------------------
SELECT setval('"public"."articlecategoryid_seq"', 1, false);

-- ----------------------------
-- Alter sequences owned by
-- ----------------------------
SELECT setval('"public"."articleid"', 1, false);

-- ----------------------------
-- Alter sequences owned by
-- ----------------------------
SELECT setval('"public"."gen_demoid_seq"', 1, false);

-- ----------------------------
-- Alter sequences owned by
-- ----------------------------
SELECT setval('"public"."gen_table_columnid_seq"', 1, false);

-- ----------------------------
-- Alter sequences owned by
-- ----------------------------
SELECT setval('"public"."gen_tableid_seq"', 1, false);

-- ----------------------------
-- Alter sequences owned by
-- ----------------------------
SELECT setval('"public"."sys_configid_seq"', 7, false);

-- ----------------------------
-- Alter sequences owned by
-- ----------------------------
SELECT setval('"public"."sys_deptid_seq"', 200, false);

-- ----------------------------
-- Alter sequences owned by
-- ----------------------------
SELECT setval('"public"."sys_dict_dataid_seq"', 34, false);

-- ----------------------------
-- Alter sequences owned by
-- ----------------------------
SELECT setval('"public"."sys_dict_typeid_seq"', 12, false);

-- ----------------------------
-- Alter sequences owned by
-- ----------------------------
SELECT setval('"public"."sys_logininforid_seq"', 4, true);

-- ----------------------------
-- Alter sequences owned by
-- ----------------------------
SELECT setval('"public"."sys_menuid_seq"', 2015, false);

-- ----------------------------
-- Alter sequences owned by
-- ----------------------------
SELECT setval('"public"."sys_noticeid_seq"', 1, false);

-- ----------------------------
-- Alter sequences owned by
-- ----------------------------
SELECT setval('"public"."sys_oper_logid_seq"', 5, true);

-- ----------------------------
-- Alter sequences owned by
-- ----------------------------
SELECT setval('"public"."sys_postid_seq"', 14, false);

-- ----------------------------
-- Alter sequences owned by
-- ----------------------------
SELECT setval('"public"."sys_roleid_seq"', 2, false);

-- ----------------------------
-- Alter sequences owned by
-- ----------------------------
SELECT setval('"public"."sys_tasks_logid_seq"', 1, false);

-- ----------------------------
-- Alter sequences owned by
-- ----------------------------
SELECT setval('"public"."sys_userid_seq"', 1, false);

-- ----------------------------
-- Primary Key structure for table article
-- ----------------------------
ALTER TABLE "public"."article" ADD CONSTRAINT "article_pkey" PRIMARY KEY ("cid");

-- ----------------------------
-- Primary Key structure for table articlecategory
-- ----------------------------
ALTER TABLE "public"."articlecategory" ADD CONSTRAINT "_copy_23" PRIMARY KEY ("category_id");

-- ----------------------------
-- Primary Key structure for table gen_demo
-- ----------------------------
ALTER TABLE "public"."gen_demo" ADD CONSTRAINT "_copy_22" PRIMARY KEY ("id");

-- ----------------------------
-- Primary Key structure for table gen_table
-- ----------------------------
ALTER TABLE "public"."gen_table" ADD CONSTRAINT "_copy_21" PRIMARY KEY ("tableid");

-- ----------------------------
-- Primary Key structure for table gen_table_column
-- ----------------------------
ALTER TABLE "public"."gen_table_column" ADD CONSTRAINT "_copy_20" PRIMARY KEY ("columnid");

-- ----------------------------
-- Primary Key structure for table sys_common_lang
-- ----------------------------
ALTER TABLE "public"."sys_common_lang" ADD CONSTRAINT "_copy_19" PRIMARY KEY ("id");

-- ----------------------------
-- Primary Key structure for table sys_config
-- ----------------------------
ALTER TABLE "public"."sys_config" ADD CONSTRAINT "_copy_18" PRIMARY KEY ("configid");

-- ----------------------------
-- Primary Key structure for table sys_dept
-- ----------------------------
ALTER TABLE "public"."sys_dept" ADD CONSTRAINT "_copy_17" PRIMARY KEY ("deptid");

-- ----------------------------
-- Primary Key structure for table sys_dict_data
-- ----------------------------
ALTER TABLE "public"."sys_dict_data" ADD CONSTRAINT "_copy_16" PRIMARY KEY ("dictcode");

-- ----------------------------
-- Indexes structure for table sys_dict_type
-- ----------------------------
CREATE UNIQUE INDEX "dictType" ON "public"."sys_dict_type" USING btree (
  "dicttype" COLLATE "pg_catalog"."default" "pg_catalog"."text_ops" ASC NULLS LAST
);

-- ----------------------------
-- Primary Key structure for table sys_dict_type
-- ----------------------------
ALTER TABLE "public"."sys_dict_type" ADD CONSTRAINT "_copy_15" PRIMARY KEY ("dictid");

-- ----------------------------
-- Primary Key structure for table sys_file
-- ----------------------------
ALTER TABLE "public"."sys_file" ADD CONSTRAINT "_copy_14" PRIMARY KEY ("id");

-- ----------------------------
-- Primary Key structure for table sys_logininfor
-- ----------------------------
ALTER TABLE "public"."sys_logininfor" ADD CONSTRAINT "_copy_13" PRIMARY KEY ("infoid");

-- ----------------------------
-- Primary Key structure for table sys_menu
-- ----------------------------
ALTER TABLE "public"."sys_menu" ADD CONSTRAINT "_copy_12" PRIMARY KEY ("menuid");

-- ----------------------------
-- Primary Key structure for table sys_notice
-- ----------------------------
ALTER TABLE "public"."sys_notice" ADD CONSTRAINT "_copy_11" PRIMARY KEY ("notice_id");

-- ----------------------------
-- Primary Key structure for table sys_oper_log
-- ----------------------------
ALTER TABLE "public"."sys_oper_log" ADD CONSTRAINT "_copy_10" PRIMARY KEY ("operid");

-- ----------------------------
-- Primary Key structure for table sys_post
-- ----------------------------
ALTER TABLE "public"."sys_post" ADD CONSTRAINT "_copy_9" PRIMARY KEY ("postid");

-- ----------------------------
-- Primary Key structure for table sys_role
-- ----------------------------
ALTER TABLE "public"."sys_role" ADD CONSTRAINT "_copy_8" PRIMARY KEY ("roleid");

-- ----------------------------
-- Primary Key structure for table sys_role_dept
-- ----------------------------
ALTER TABLE "public"."sys_role_dept" ADD CONSTRAINT "_copy_7" PRIMARY KEY ("roleid", "deptid");

-- ----------------------------
-- Primary Key structure for table sys_role_menu
-- ----------------------------
ALTER TABLE "public"."sys_role_menu" ADD CONSTRAINT "_copy_6" PRIMARY KEY ("role_id", "menu_id");

-- ----------------------------
-- Primary Key structure for table sys_tasks
-- ----------------------------
ALTER TABLE "public"."sys_tasks" ADD CONSTRAINT "_copy_5" PRIMARY KEY ("id");

-- ----------------------------
-- Primary Key structure for table sys_tasks_log
-- ----------------------------
ALTER TABLE "public"."sys_tasks_log" ADD CONSTRAINT "_copy_4" PRIMARY KEY ("joblogid");

-- ----------------------------
-- Primary Key structure for table sys_user
-- ----------------------------
ALTER TABLE "public"."sys_user" ADD CONSTRAINT "_copy_3" PRIMARY KEY ("userid");

-- ----------------------------
-- Primary Key structure for table sys_user_post
-- ----------------------------
ALTER TABLE "public"."sys_user_post" ADD CONSTRAINT "_copy_2" PRIMARY KEY ("userid", "postid");

-- ----------------------------
-- Primary Key structure for table sys_user_role
-- ----------------------------
ALTER TABLE "public"."sys_user_role" ADD CONSTRAINT "_copy_1" PRIMARY KEY ("user_id", "role_id");
