using System;
using System.Collections.Generic;
using System.Text;

namespace ZR.Model.System
{
    public class UserConstants
    {
        /**
    * 平台内系统用户的唯一标志
    */
        public static string SYS_USER = "SYS_USER";

        /** 正常状态 */
        public static string NORMAL = "0";

        /** 异常状态 */
        public static string EXCEPTION = "1";

        /** 用户封禁状态 */
        public static string USER_DISABLE = "1";

        /** 角色封禁状态 */
        public static string ROLE_DISABLE = "1";

        /** 部门正常状态 */
        public static string DEPT_NORMAL = "0";

        /** 部门停用状态 */
        public static string DEPT_DISABLE = "1";

        /** 字典正常状态 */
        public static string DICT_NORMAL = "0";

        /** 是否为系统默认（是） */
        public static string YES = "Y";

        /** 是否菜单外链（是） */
        public static string YES_FRAME = "1";

        /** 是否菜单外链（否） */
        public static string NO_FRAME = "0";

        /** 菜单类型（目录） */
        public static string TYPE_DIR = "M";

        /** 菜单类型（菜单） */
        public static string TYPE_MENU = "C";

        /** 菜单类型（按钮） */
        public static string TYPE_BUTTON = "F";
        /** 菜单类型（链接） */
        //public static string TYPE_LINK = "L";

        /** Layout组件标识 */
        public static string LAYOUT = "Layout";
        /** ParentView组件标识 */
        public static string PARENT_VIEW = "ParentView";
        /** InnerLink组件标识 */
        public static string INNER_LINK = "InnerLink";
        /** 校验返回结果码 */
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
    }
}
