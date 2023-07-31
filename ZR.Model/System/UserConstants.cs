namespace ZR.Model.System
{
    public class UserConstants
    {
        /// <summary>
        /// 平台内系统用户的唯一标志
        /// </summary>
        public static string SYS_USER = "SYS_USER";

        /// <summary>
        /// 正常状态
        /// </summary>
        public static string NORMAL = "0";

        /// <summary>
        /// 异常状态
        /// </summary>
        public static string EXCEPTION = "1";

        /// <summary>
        /// 用户封禁状态
        /// </summary>
        public static string USER_DISABLE = "1";

        /// <summary>
        /// 角色封禁状态
        /// </summary>
        public static string ROLE_DISABLE = "1";

        /// <summary>
        /// 部门正常状态
        /// </summary>
        public static int DEPT_NORMAL = 0;

        /// <summary>
        /// 部门停用状态
        /// </summary>
        public static string DEPT_DISABLE = "1";

        /// <summary>
        /// 字典正常状态
        /// </summary>
        public static string DICT_NORMAL = "0";

        /// <summary>
        /// 是否为系统默认（是）
        /// </summary>
        public static string YES = "Y";

        /// <summary>
        /// 是否菜单外链（是）
        /// </summary>
        public static string YES_FRAME = "1";

        /// <summary>
        /// 是否菜单外链（否）
        /// </summary>
        public static string NO_FRAME = "0";

        /// <summary>
        /// 菜单类型（目录）
        /// </summary>
        public static string TYPE_DIR = "M";

        /// <summary>
        /// 菜单类型（菜单）
        /// </summary>
        public static string TYPE_MENU = "C";

        /// <summary>
        /// 菜单类型（按钮）
        /// </summary>
        public static string TYPE_BUTTON = "F";

        ///// <summary>
        ///// 菜单类型（链接）
        ///// </summary>
        //public static string TYPE_LINK = "L";

        /// <summary>
        /// Layout组件标识
        /// </summary>
        public static string LAYOUT = "Layout";

        /// <summary>
        /// ParentView组件标识
        /// </summary>
        public static string PARENT_VIEW = "ParentView";

        /// <summary>
        /// InnerLink组件标识
        /// </summary>
        public static string INNER_LINK = "InnerLink";

        /// <summary>
        /// 校验返回结果码
        /// </summary>
        public static string UNIQUE = "0";
        public static string NOT_UNIQUE = "1";

        /// <summary>
        /// http请求
        /// </summary>
        public static string HTTP = "http://";

        /// <summary>
        /// https请求
        /// </summary>
        public static string HTTPS = "https://";
        /// <summary>
        /// www主域
        /// </summary>
        public static string WWW = "www.";
    }
}
