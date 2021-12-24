

namespace ZR.CodeGenerator
{
    /// <summary>
    /// 代码生成常量
    /// </summary>
    public class GenConstants
    {
        public static string Gen_conn = "gen:conn";
        public static string Gen_conn_dbType = "gen:dbType";
        public static string Gen_author = "gen:author";
        public static string Gen_autoPre = "gen:autoPre";
        public static string Gen_tablePrefix = "gen:tablePrefix";

        /// <summary>
        /// InputDto输入实体是不包含字段
        /// </summary>
        public static readonly string[] inputDtoNoField = new string[] { "createTime", "updateTime", "addtime", "create_time", "update_time" };
        /// <summary>
        /// 图片字段
        /// </summary>
        public static readonly string[] imageFiled = new string[] { "icon", "img", "image", "url", "pic", "photo", "avatar" };
        /// <summary>
        /// 下拉框字段
        /// </summary>
        public static readonly string[] selectFiled = new string[] { "status", "type", "state", "sex", "gender" };
        /// <summary>
        /// 单选按钮字段
        /// </summary>
        public static readonly string[] radioFiled = new string[] { "status", "state", "is"};


        /** 单表（增删改查） */
        public static string TPL_CRUD = "crud";

        /** 树表（增删改查） */
        public static string TPL_TREE = "tree";

        /** 主子表（增删改查） */
        public static string TPL_SUB = "sub";

        /** 树编码字段 */
        public static string TREE_CODE = "treeCode";

        /** 树父编码字段 */
        public static string TREE_PARENT_CODE = "treeParentCode";

        /** 树名称字段 */
        public static string TREE_NAME = "treeName";

        /** 上级菜单ID字段 */
        public static string PARENT_MENU_ID = "parentMenuId";

        /** 上级菜单名称字段 */
        public static string PARENT_MENU_NAME = "parentMenuName";

        /** 数据库字符串类型 */
        public static string[] COLUMNTYPE_STR = { "char", "varchar", "nvarchar", "varchar2" };

        /** 数据库文本类型 */
        public static string[] COLUMNTYPE_TEXT = { "tinytext", "text", "mediumtext", "longtext" };

        /** 数据库时间类型 */
        public static string[] COLUMNTYPE_TIME = { "datetime", "time", "date", "timestamp" };

        /** 页面不需要编辑字段 */
        public static string[] COLUMNNAME_NOT_EDIT = { "id", "create_by", "create_time", "delFlag" };

        /** 页面不需要显示的列表字段 */
        public static string[] COLUMNNAME_NOT_LIST = { "create_by", "create_time", "delFlag", "update_by",
            "update_time" , "password"};

        /** 页面不需要查询字段 */
        public static string[] COLUMNNAME_NOT_QUERY = { "id", "create_by", "create_time", "delFlag", "update_by",
            "update_time", "remark" };

        /** Entity基类字段 */
        public static string[] BASE_ENTITY = { "createBy", "createTime", "updateBy", "updateTime", "remark" };

        /** Tree基类字段 */
        public static string[] TREE_ENTITY = { "parentName", "parentId", "orderNum", "ancestors", "children" };

        /** 文本框 */
        public static string HTML_INPUT = "input";
        /** 数字框 */
        public static string HTML_INPUT_NUMBER = "inputNumber";

        /** 文本域 */
        public static string HTML_TEXTAREA = "textarea";

        /** 下拉框 */
        public static string HTML_SELECT = "select";

        /** 单选框 */
        public static string HTML_RADIO = "radio";

        /** 复选框 */
        public static string HTML_CHECKBOX = "checkbox";

        /** 日期控件 */
        public static string HTML_DATETIME = "datetime";

        /** 图片上传控件 */
        public static string HTML_IMAGE_UPLOAD = "imageUpload";

        /** 文件上传控件 */
        public static string HTML_FILE_UPLOAD = "fileUpload";

        /** 富文本控件 */
        public static string HTML_EDITOR = "editor";
        // 自定义排序
        public static string HTML_SORT = "sort";
        /// <summary>
        /// 自定义输入框
        /// </summary>
        public static string HTML_CUSTOM_INPUT = "customInput";
        //颜色选择器
        public static string HTML_COLORPICKER = "colorPicker";
        //switch开关
        public static string HTML_SWITCH { get; set; }

        /** 字符串类型 */
        public static string TYPE_STRING = "string";

        /** 整型 */
        public static string TYPE_INT = "int";

        /** 长整型 */
        public static string TYPE_LONG = "long";

        /** 浮点型 */
        public static string TYPE_DOUBLE = "Double";

        /** 时间类型 */
        public static string TYPE_DATE = "DateTime";

        /** 模糊查询 */
        public static string QUERY_LIKE = "LIKE";

        /** 需要 */
        public static string REQUIRE = "1";
    }
}