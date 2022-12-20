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
-- Records of sys_dept
-- ----------------------------
INSERT INTO "public"."sys_dept" VALUES (100, 0, '0', 'A公司', 0, 'zr', '', '', '0', '0', 'admin', NULL, '', NULL, NULL);
INSERT INTO "public"."sys_dept" VALUES (101, 100, '0,100', '研发部门', 1, 'zr', '', '', '0', '0', 'admin', NULL, '', NULL, NULL);
INSERT INTO "public"."sys_dept" VALUES (102, 100, '0,100', '市场部门', 2, 'zr', '', '', '0', '0', 'admin', NULL, '', NULL, NULL);
INSERT INTO "public"."sys_dept" VALUES (103, 100, '0,100', '测试部门', 3, 'zr', '', '', '0', '0', 'admin', NULL, '', NULL, NULL);
INSERT INTO "public"."sys_dept" VALUES (104, 100, '0,100', '财务部门', 4, 'zr', '', '', '0', '0', 'admin', NULL, '', NULL, NULL);
INSERT INTO "public"."sys_dept" VALUES (200, 0, '0', 'B公司', 0, 'zr', '', '', '0', '0', 'admin', NULL, '', NULL, NULL);

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
-- Records of sys_file
-- ----------------------------

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
-- Records of sys_logininfor
-- ----------------------------
INSERT INTO "public"."sys_logininfor" VALUES (1, 'admin', '127.0.0.1', '0-内网IP', 'Windows 10 Other Chrome 86.0.4240', 'Windows 10', '1', '用户名或密码错误', '2022-12-20 15:04:17.41581');
INSERT INTO "public"."sys_logininfor" VALUES (2, 'admin', '127.0.0.1', '0-内网IP', 'Windows 10 Other Chrome 86.0.4240', 'Windows 10', '1', '用户名或密码错误', '2022-12-20 15:06:07.623798');
INSERT INTO "public"."sys_logininfor" VALUES (3, 'admin', '127.0.0.1', '0-内网IP', 'Windows 10 Other Chrome 86.0.4240', 'Windows 10', '0', '登录成功', '2022-12-20 15:07:46.94624');
INSERT INTO "public"."sys_logininfor" VALUES (4, 'admin', '127.0.0.1', '0-内网IP', 'Windows 10 Other Chrome 86.0.4240', 'Windows 10', '0', '登录成功', '2022-12-20 15:07:58.713711');

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
-- Records of sys_notice
-- ----------------------------

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
-- Records of sys_oper_log
-- ----------------------------
INSERT INTO "public"."sys_oper_log" VALUES (1, NULL, 0, NULL, 'POST', 0, NULL, NULL, '/login', '127.0.0.1', '0 内网IP', '{"username":"admin","password":"123456","code":"2t37","uuid":"8ef694174eb94ce6ba45cbd341996c8e"}', '{
  "code": 105,
  "msg": "用户名或密码错误",
  "data": null
}', 1, '用户名或密码错误', '2022-12-20 15:04:23.595226', 0);
INSERT INTO "public"."sys_oper_log" VALUES (2, NULL, 0, NULL, 'POST', 0, NULL, NULL, '/login', '127.0.0.1', '0 内网IP', '{"username":"admin","password":"123456","code":"5ewk","uuid":"88429631d73244cd8e001b480cb191d4"}', '{
  "code": 105,
  "msg": "用户名或密码错误",
  "data": null
}', 1, '用户名或密码错误', '2022-12-20 15:06:08.377878', 0);
INSERT INTO "public"."sys_oper_log" VALUES (3, '操作日志', 5, 'SysOperlog.Export()', 'GET', 0, 'admin', NULL, '/monitor/OperLog/export', '127.0.0.1', '0 内网IP', '?pageNum=1&pageSize=20', '{  "code": 200,  "msg": "SUCCESS",  "data": {    "path": "/export/操作日志12-20-155331.xlsx",    "fileName": "操作日志12-20-155331.xlsx"  }}', 0, NULL, '2022-12-20 15:53:31.155256', 0);
INSERT INTO "public"."sys_oper_log" VALUES (4, '操作日志', 5, 'SysOperlog.Export()', 'GET', 0, 'admin', NULL, '/monitor/OperLog/export', '127.0.0.1', '0 内网IP', '?pageNum=1&pageSize=20', '{  "code": 200,  "msg": "SUCCESS",  "data": {    "path": "/export/操作日志12-20-155348.xlsx",    "fileName": "操作日志12-20-155348.xlsx"  }}', 0, NULL, '2022-12-20 15:53:48.062228', 0);
INSERT INTO "public"."sys_oper_log" VALUES (5, '操作日志', 5, 'SysOperlog.Export()', 'GET', 0, 'admin', NULL, '/monitor/OperLog/export', '127.0.0.1', '0 内网IP', '?pageNum=1&pageSize=20', '{  "code": 200,  "msg": "SUCCESS",  "data": {    "path": "/export/操作日志12-20-155716.xlsx",    "fileName": "操作日志12-20-155716.xlsx"  }}', 0, NULL, '2022-12-20 15:57:29.892171', 0);

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
-- Table structure for sys_role
-- ----------------------------
DROP TABLE IF EXISTS "public"."sys_role";
CREATE TABLE "public"."sys_role" (
  "roleid" int8 NOT NULL DEFAULT nextval('sys_roleid_seq'::regclass),
  "rolename" varchar(30) COLLATE "pg_catalog"."default" NOT NULL,
  "rolekey" varchar(100) COLLATE "pg_catalog"."default" NOT NULL,
  "rolesort" int4 NOT NULL,
  "datascope" char(1) COLLATE "pg_catalog"."default",
  "menu_check_strictly" int2,
  "dept_check_strictly" int2 NOT NULL,
  "status" char(1) COLLATE "pg_catalog"."default" NOT NULL,
  "delflag" char(1) COLLATE "pg_catalog"."default" NOT NULL,
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
-- Records of sys_role
-- ----------------------------
INSERT INTO "public"."sys_role" VALUES (1, '超级管理员', 'admin', 1, '1', 1, 0, '0', '0', 'admin', '2022-12-19 10:12:36', 'system', NULL, '超级管理员');
INSERT INTO "public"."sys_role" VALUES (2, '普通角色', 'common', 2, '2', 1, 0, '0', '0', 'admin', '2022-12-19 10:12:36', 'system', NULL, '普通角色');

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
-- Records of sys_role_dept
-- ----------------------------
INSERT INTO "public"."sys_role_dept" VALUES (2, 100);
INSERT INTO "public"."sys_role_dept" VALUES (2, 101);
INSERT INTO "public"."sys_role_dept" VALUES (2, 105);

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
INSERT INTO "public"."sys_role_menu" VALUES (3, 4, NULL, '2022-12-19 10:12:37');
INSERT INTO "public"."sys_role_menu" VALUES (3, 118, NULL, '2022-12-19 10:12:37');
INSERT INTO "public"."sys_role_menu" VALUES (3, 1047, NULL, '2022-12-19 10:12:37');
INSERT INTO "public"."sys_role_menu" VALUES (3, 1048, NULL, '2022-12-19 10:12:37');
INSERT INTO "public"."sys_role_menu" VALUES (3, 1049, NULL, '2022-12-19 10:12:37');
INSERT INTO "public"."sys_role_menu" VALUES (3, 1050, NULL, '2022-12-19 10:12:37');

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
-- Records of sys_tasks
-- ----------------------------

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
-- Records of sys_tasks_log
-- ----------------------------

-- ----------------------------
-- Table structure for sys_user
-- ----------------------------
DROP TABLE IF EXISTS "public"."sys_user";
CREATE TABLE "public"."sys_user" (
  "userid" int8 NOT NULL DEFAULT nextval('sys_userid_seq'::regclass),
  "deptid" int8,
  "username" varchar(30) COLLATE "pg_catalog"."default" NOT NULL,
  "nickname" varchar(30) COLLATE "pg_catalog"."default" NOT NULL,
  "usertype" varchar(2) COLLATE "pg_catalog"."default",
  "email" varchar(50) COLLATE "pg_catalog"."default",
  "phonenumber" varchar(11) COLLATE "pg_catalog"."default",
  "sex" char(1) COLLATE "pg_catalog"."default",
  "avatar" varchar(400) COLLATE "pg_catalog"."default",
  "password" varchar(100) COLLATE "pg_catalog"."default",
  "status" char(1) COLLATE "pg_catalog"."default",
  "delflag" char(1) COLLATE "pg_catalog"."default",
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
-- Records of sys_user
-- ----------------------------
INSERT INTO "public"."sys_user" VALUES (3, 100, 'editor', '编辑人员', '0', NULL, NULL, '2', '', 'E10ADC3949BA59ABBE56E057F20F883E', '0', '0', '127.0.0.1', '2021-08-19 09:27:46', 'admin', '2021-08-18 18:24:53', '', NULL, NULL);
INSERT INTO "public"."sys_user" VALUES (2, 0, 'zr', 'zr', '0', NULL, NULL, '0', '', 'E10ADC3949BA59ABBE56E057F20F883E', '0', '0', '', '0001-01-01 00:00:00', 'admin', '2021-07-05 17:29:13', 'admin', '2021-08-02 16:53:04', '普通用户');
INSERT INTO "public"."sys_user" VALUES (1, 0, 'admin', '管理员', '0', '', '', '0', '', 'E10ADC3949BA59ABBE56E057F20F883E', '0', '0', '127.0.0.1', '2022-12-20 15:07:58.729401', 'admin', '2020-11-26 11:52:59', 'admin', '2021-08-03 10:11:24', '管理员');

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
-- Records of sys_user_post
-- ----------------------------
INSERT INTO "public"."sys_user_post" VALUES (1, 1);
INSERT INTO "public"."sys_user_post" VALUES (4, 4);

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
-- Records of sys_user_role
-- ----------------------------
INSERT INTO "public"."sys_user_role" VALUES (1, 1);
INSERT INTO "public"."sys_user_role" VALUES (2, 2);
INSERT INTO "public"."sys_user_role" VALUES (3, 3);
INSERT INTO "public"."sys_user_role" VALUES (4, 2);
INSERT INTO "public"."sys_user_role" VALUES (101, 2);
INSERT INTO "public"."sys_user_role" VALUES (109, 116);
INSERT INTO "public"."sys_user_role" VALUES (110, 2);

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
