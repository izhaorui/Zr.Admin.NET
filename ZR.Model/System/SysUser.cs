using MiniExcelLibs.Attributes;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections.Generic;

namespace ZR.Model.System
{
    /// <summary>
    /// 用户表
    /// </summary>
    [SugarTable("sys_user")]
    [Tenant("0")]
    public class SysUser : SysBase
    {
        /// <summary>
        /// 用户id
        /// </summary>
        [SugarColumn(IsIdentity = true, IsPrimaryKey = true)]
        public long UserId { get; set; }
        /// <summary>
        /// 登录用户名
        /// </summary>
        [SugarColumn(Length = 30, ColumnDescription = "用户账号", ColumnDataType = "varchar")]
        public string UserName { get; set; }
        /// <summary>
        /// 用户昵称
        /// </summary>
        [SugarColumn(Length = 30, ColumnDescription = "用户昵称", ColumnDataType = "varchar")]
        public string NickName { get; set; }
        /// <summary>
        /// 用户类型（00系统用户）
        /// </summary>
        [SugarColumn(Length = 2, ColumnDescription = "用户类型（00系统用户）", ColumnDataType = "varchar", DefaultValue = "00")]
        public string UserType { get; set; } = "00";
        //[SugarColumn(IsOnlyIgnoreInsert = true)]
        public string Avatar { get; set; }
        [SugarColumn(Length = 50, ColumnDescription = "用户邮箱", ColumnDataType = "varchar")]
        public string Email { get; set; }

        [JsonIgnore]
        [ExcelIgnore]
        [SugarColumn(Length = 100, ColumnDescription = "密码", ColumnDataType = "varchar")]
        public string Password { get; set; }
        /// <summary>
        /// 手机号
        /// </summary>
        public string Phonenumber { get; set; }
        /// <summary>
        /// 用户性别（0男 1女 2未知）
        /// </summary>
        public int Sex { get; set; }

        /// <summary>
        /// 帐号状态（0正常 1停用）
        /// </summary>
        [ExcelIgnore]
        public int Status { get; set; }

        /// <summary>
        /// 删除标志（0代表存在 2代表删除）
        /// </summary>
        //[SugarColumn(IsOnlyIgnoreInsert = true)]
        public int DelFlag { get; set; }

        /// <summary>
        /// 最后登录IP
        /// </summary>
        [SugarColumn(IsOnlyIgnoreInsert = true)]
        public string LoginIP { get; set; }

        /// <summary>
        /// 最后登录时间
        /// </summary>
        [SugarColumn(IsOnlyIgnoreInsert = true)]
        [ExcelColumn(Name = "登录日期", Format = "yyyy-MM-dd HH:mm:ss")]
        public DateTime? LoginDate { get; set; }

        /// <summary>
        /// 部门Id
        /// </summary>
        public long DeptId { get; set; }

        #region 表额外字段
        public bool IsAdmin()
        {
            return IsAdmin(UserId);
        }
        public static bool IsAdmin(long userId)
        {
            return 1 == userId;
        }

        /// <summary>
        /// 拥有角色个数
        /// </summary>
        //[SugarColumn(IsIgnore = true)]
        //public int RoleNum { get; set; }
        [SugarColumn(IsIgnore = true)]
        public string DeptName { get; set; }
        /// <summary>
        /// 角色id集合
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        [ExcelIgnore]
        public long[] RoleIds { get; set; }
        /// <summary>
        /// 岗位集合
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        [ExcelIgnore]
        public int[] PostIds { get; set; }

        [SugarColumn(IsIgnore = true)]
        [ExcelIgnore]
        public List<SysRole> Roles { get; set; }
        [SugarColumn(IsIgnore = true)]
        public string WelcomeMessage
        {
            get
            {
                int now = DateTime.Now.Hour;

                if (now > 0 && now <= 6)
                {
                    return "午夜好";
                }
                else if (now > 6 && now <= 11)
                {
                    return "早上好";
                }
                else if (now > 11 && now <= 14)
                {
                    return "中午好";
                }
                else if (now > 14 && now <= 18)
                {
                    return "下午好";
                }
                else
                {
                    return "晚上好";
                }
            }
        }
        [SugarColumn(IsIgnore = true)]
        public string WelcomeContent { get; set; }

        #endregion
    }
}
